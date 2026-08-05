using System;
using System.Buffers;
using System.IO;
using System.Threading;
using MessagePack;

namespace Ink_Canvas.UInk
{
    /// <summary>
    /// UInk 主文件读取器。读取连续 MessagePack 对象流，重建 <see cref="UInkDocument"/>。
    /// 容错规则（对应规范 uink_conf / uink_inc）：
    ///  - 首块必须是 array(7) Header（type=0, version=10），否则视为非 UInk 文件（返回 null）；
    ///  - 未知 Type ID 跳过（作为完整对象已消费）；
    ///  - EOF 处不完整尾块丢弃（保留此前完整对象）；
    ///  - 解码失败停止读取，不重新同步。
    /// </summary>
    public static class UInkReader
    {
        public static UInkDocument Load(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Load(fs);
        }

        public static UInkDocument Load(Stream stream)
        {
            var doc = new UInkDocument();
            bool headerSeen = false;
            UInkCanvasRecord current = null;

            using (var sr = new MessagePackStreamReader(stream))
            {
                while (true)
                {
                    ReadOnlySequence<byte> blockBytes;
                    try
                    {
                        var read = sr.ReadAsync(CancellationToken.None).GetAwaiter().GetResult();
                        if (read == null)
                        {
                            // EOF。RemainingBytes 非空 = 截断尾块，丢弃。
                            break;
                        }
                        blockBytes = read.Value;
                    }
                    catch (Exception)
                    {
                        // 流读取异常视为损坏：停止读取，保留此前完整对象。
                        break;
                    }

                    if (!headerSeen)
                    {
                        // 首块校验：array(7) Header
                        UInkHeader header;
                        try
                        {
                            var first = new MessagePackReader(blockBytes);
                            if (!IsArrayHeader(ref first, out int len) || len != 7)
                                return null;
                            // 第一元素必须为整数 0（type）
                            if (!UInkFmt.IsInteger(ref first)) return null;
                            if (first.ReadInt32() != (int)UInkBlockType.Header) return null;
                            var hr = new MessagePackReader(blockBytes);
                            header = UInkHeaderFormatter.Instance.Deserialize(ref hr, UInkSerializer.Options);
                            if (header.Version != 10 || header.Type != 0) return null;
                        }
                        catch (Exception)
                        {
                            return null;
                        }
                        doc.Header = header;
                        headerSeen = true;
                        continue;
                    }

                    // 后续块：按 Type ID 分发
                    UInkBlockType type;
                    try
                    {
                        type = PeekType(blockBytes);
                    }
                    catch (Exception)
                    {
                        break; // 无法判定类型 → 停止读取
                    }

                    try
                    {
                        switch (type)
                        {
                            case UInkBlockType.Header:
                                // 重复 Header → 结构非法，停止读取
                                return null;
                            case UInkBlockType.HeaderExtension:
                                if (doc.HeaderExtension != null) return null; // 最多 1 个
                                {
                                    var er = new MessagePackReader(blockBytes);
                                    doc.HeaderExtension = UInkHeaderExtensionFormatter.Instance.Deserialize(ref er, UInkSerializer.Options);
                                }
                                current = null;
                                break;
                            case UInkBlockType.Canvas:
                                {
                                    var cr = new MessagePackReader(blockBytes);
                                    var canvas = UInkCanvasFormatter.Instance.Deserialize(ref cr, UInkSerializer.Options);
                                    current = new UInkCanvasRecord { Canvas = canvas };
                                    doc.Canvases.Add(current);
                                }
                                break;
                            case UInkBlockType.Ink:
                                {
                                    if (current == null) return null; // 首个 Canvas 前不得出现内容块
                                    var ir = new MessagePackReader(blockBytes);
                                    current.Blocks.Add(UInkInkFormatter.Instance.Deserialize(ref ir, UInkSerializer.Options));
                                }
                                break;
                            case UInkBlockType.Shape:
                                {
                                    if (current == null) return null;
                                    var sr2 = new MessagePackReader(blockBytes);
                                    current.Blocks.Add(UInkShapeFormatter.Instance.Deserialize(ref sr2, UInkSerializer.Options));
                                }
                                break;
                            case UInkBlockType.Media:
                                {
                                    if (current == null) return null;
                                    var mr = new MessagePackReader(blockBytes);
                                    current.Blocks.Add(UInkMediaFormatter.Instance.Deserialize(ref mr, UInkSerializer.Options));
                                }
                                break;
                            default:
                                // 未知 Type ID：作为完整对象已消费，跳过继续。
                                break;
                        }
                    }
                    catch (Exception)
                    {
                        // 块解码失败 → 停止读取，保留此前完整对象。
                        break;
                    }
                }
            }

            return headerSeen ? doc : null;
        }

        /// <summary>读取块类型：Header 是 array，其余是带 "type" 键的 Map。</summary>
        private static UInkBlockType PeekType(ReadOnlySequence<byte> bytes)
        {
            var r = new MessagePackReader(bytes);
            if (IsArrayHeader(ref r, out _))
                return UInkBlockType.Header;

            if (!IsMapHeader(ref r, out int count))
                throw new MessagePackSerializationException("UInk 块既不是 array 也不是 map");

            for (int i = 0; i < count; i++)
            {
                var key = r.ReadString();
                if (key == "type")
                {
                    return (UInkBlockType)UInkFmt.ReadInt32Tolerant(ref r);
                }
                r.Skip();
            }
            throw new MessagePackSerializationException("UInk Map 块缺少 type 键");
        }

        private static bool IsArrayHeader(ref MessagePackReader r, out int count)
        {
            var code = r.NextCode;
            if (code == MessagePackCode.Array16 || code == MessagePackCode.Array32)
            {
                count = r.ReadArrayHeader();
                return true;
            }
            if (code >= 0x90 && code <= 0x9F)
            {
                count = r.ReadArrayHeader();
                return true;
            }
            count = 0;
            return false;
        }

        private static bool IsMapHeader(ref MessagePackReader r, out int count)
        {
            var code = r.NextCode;
            if (code == MessagePackCode.Map16 || code == MessagePackCode.Map32)
            {
                count = r.ReadMapHeader();
                return true;
            }
            if (code >= 0x80 && code <= 0x8F)
            {
                count = r.ReadMapHeader();
                return true;
            }
            count = 0;
            return false;
        }
    }
}
