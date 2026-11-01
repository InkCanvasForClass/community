#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Ink_Canvas.Helpers;

namespace Ink_Canvas.Ink;

/// <summary>
/// 把「一帧原始触点」合成「稳定的手指事件」。
///
/// <para><b>为什么需要它</b>：有些触摸屏标称 10 指但只有 6 个通道，不足的通道由多根手指
/// <b>交替上报</b> —— 所以槽位号（LinkCollection）只能当"运输容器"，绝不能当手指身份。
/// 身份按这个优先级取：① 设备报了 Contact Identifier(0x0D:0x51) → 直接用 CID
/// （<b>CID=0 也是有效身份</b>，这台屏 L1 恒为 0；只有"本帧所有触点的 CID 都一样"时才认为该 CID 不可用）；
/// ② 否则（例如 Microsoft HID RID 屏根本没声明 CID）→ 按位置做相邻帧最近邻跟踪，分配 StableId。</para>
///
/// <para><b>两条不能违反的经验</b>：位置<b>绝不</b>滤波（滤波会滞后约 2 帧，导致末梢漏点）；
/// 宽高做中值滤波（稳定手掌/书写判定）。</para>
/// </summary>
internal sealed class ContactTracker
{
    public enum Kind { Down, Move, Up }

    /// <summary>一帧里的一根触点（来自 RawInput）。Transport = LinkCollection 槽位，只当容器。</summary>
    public readonly record struct TrackIn(int Transport, bool HasCid, int ContactId, bool Down,
        float X, float Y, float W, float H, float Pressure01);

    /// <summary>合成后交给上层的"手指事件"。StableId 在按下→抬起期间保持不变，且恒 &gt;= 1（0 留给鼠标）。</summary>
    public readonly record struct ContactEvent(int StableId, Kind Kind,
        float X, float Y, float W, float H, float Pressure01);

    private const int MedianWindow = 5;

    /// <summary>最近邻匹配半径（px）。&lt;=0 表示自动：min(屏宽,屏高) × 6%。</summary>
    public float MatchRadiusPx { get; set; }

    /// <summary>连续多少帧没出现才认为抬手（配合 CountZero 用）。</summary>
    public int HoldFrames { get; set; } = 3;

    /// <summary>抬手的最长容忍时间（ms）：超过就强制抬手，避免通道交替把手指"扣住"不放。</summary>
    public int TtlMs { get; set; } = 120;

    /// <summary>
    /// 轨道"还能被匹配"的最长静默（ms）。超过这个时间没被任何一帧更新过，就<b>不再参与匹配</b>。
    /// <para>为什么必须这么狠：手指抬起后如果只靠 <see cref="TtlMs"/> 回收，快速连写时下一笔的
    /// Down 会被最近邻/CID 匹配成上一笔的 Move —— 表现就是"上一笔的抬笔点和下一笔的落笔点连成一条线"。
    /// 真实手指每隔几毫秒就报一次，静默这么久只可能是抬起来了。</para>
    /// </summary>
    public int StaleMatchMs { get; set; } = 60;

    private sealed class Track
    {
        public int StableId;
        public bool HasCid;        // 设备报过 Contact Identifier（此时 Cid=0 也算有效身份）
        public int Cid;
        public int Transport = -1;
        public float X, Y, Pressure01;
        public readonly Queue<float> Ws = new();
        public readonly Queue<float> Hs = new();
        public int MissingFrames;
        public long LastSeenMs;
        public bool LiftSent;      // 已发过 Move(补位)，避免重复
    }

    private readonly float _autoRadius;
    private readonly Dictionary<int, Track> _byStable = new();   // StableId → Track
    /// <summary>Contact Identifier → StableId：设备报了 CID 时用它做真身份（_byStable 的键是自增 StableId，不是 CID）。</summary>
    private readonly Dictionary<int, int> _cidToStable = new();
    private int _nextId = 1;
    private int _newTracks;

    public int ActiveCount => _byStable.Count;
    public int NewTrackCount => _newTracks;

    public ContactTracker(int screenW, int screenH)
    {
        _autoRadius = MathF.Max(24f, MathF.Min(screenW, screenH) * 0.06f);
    }

    private float Radius => MatchRadiusPx > 0 ? MatchRadiusPx : _autoRadius;

    public void Reset()
    {
        _byStable.Clear();
        _cidToStable.Clear();
        _nextId = 1;
        _newTracks = 0;
    }

    public string Describe() =>
        $"轨道={_byStable.Count} 新分配={_newTracks} 半径={Radius:0}px 保持={HoldFrames}帧/{TtlMs}ms";

    /// <summary>
    /// 处理「一帧」原始触点。countZero = true 表示设备明确报了「接触数 0x0D:0x54 == 0」= 整体抬手。
    /// outEvents 里的事件顺序固定为 Down → Move → Up。
    /// </summary>
    public void Sync(IReadOnlyList<TrackIn> contacts, bool countZero, List<ContactEvent> outEvents)
    {
        long now = Environment.TickCount64;
        outEvents.Clear();
        var live = new List<TrackIn>();
        var lift = new List<TrackIn>();
        foreach (TrackIn c in contacts)
            (c.Down ? live : lift).Add(c);

        // 同一帧里出现重复 CID → 这台设备的 CID 不是真身份（典型：所有槽位恒报 0）。
        // 只对这些 CID 退回最近邻，其余照常用 CID（CID=0 本身完全可以是唯一合法身份）。
        var dupCids = new HashSet<int>();
        var seenCids = new HashSet<int>();
        foreach (TrackIn c in contacts)
        {
            if (!c.HasCid)
                continue;
            if (!seenCids.Add(c.ContactId))
                dupCids.Add(c.ContactId);
        }
        if (dupCids.Count > 0)
        {
            foreach (Track t in _byStable.Values)
            {
                if (t.HasCid && dupCids.Contains(t.Cid))
                {
                    Forget(t);
                    t.HasCid = false;
                }
            }
        }

        var matched = new HashSet<int>();

        // ① 按下 / 移动
        foreach (TrackIn c in live)
        {
            Track? t = Match(c, matched, dupCids);
            if (t is null)
            {
                t = new Track { StableId = NextStableId(), Transport = c.Transport };
                _byStable[t.StableId] = t;
                BindCid(t, c, dupCids);
                _newTracks++;
                Seed(t, c);
                outEvents.Add(Event(t, Kind.Down));
            }
            else
            {
                t.Transport = c.Transport;
                if (!t.HasCid)
                    BindCid(t, c, dupCids);     // 之前没有身份，这次有了：补绑
                TrackSample(t, c);
                outEvents.Add(Event(t, Kind.Move));
            }
            t.MissingFrames = 0;
            t.LastSeenMs = now;
            t.LiftSent = false;
            matched.Add(t.StableId);
        }

        // ② 抬手过渡帧（设备把 TipSwitch 清零但坐标/尺寸还在）：先补一个 Move 把末点画出来，再抬。
        foreach (TrackIn c in lift)
        {
            Track? t = Match(c, matched, dupCids, relaxed: true);
            if (t is null)
                continue;
            if (!t.LiftSent && c.X != 0 && c.Y != 0)
            {
                t.X = c.X;
                t.Y = c.Y;
                TrackSample(t, c);
                outEvents.Add(Event(t, Kind.Move));   // 末点补位：画到真实抬手位置
                t.LiftSent = true;
            }
            outEvents.Add(Event(t, Kind.Up));
            matched.Add(t.StableId);
            Forget(t);
            _byStable.Remove(t.StableId);
        }

        // ③ 这一帧没被匹配到的轨道：countZero 或超过保持窗口才真抬手
        var toRemove = new List<int>();
        foreach (KeyValuePair<int, Track> kv in _byStable)
        {
            Track t = kv.Value;
            if (matched.Contains(t.StableId))
                continue;

            t.MissingFrames++;
            bool giveUp = countZero ||
                (t.MissingFrames >= HoldFrames && now - t.LastSeenMs > TtlMs);
            if (!giveUp)
                continue;

            outEvents.Add(Event(t, Kind.Up));
            toRemove.Add(t.StableId);
        }
        foreach (int id in toRemove)
        {
            if (_byStable.TryGetValue(id, out Track? t))
                Forget(t);
            _byStable.Remove(id);
        }

    }

    /// <summary>轨道下线时一并清掉 CID 绑定，避免 CID 被复用到别的轨道上。</summary>
    private void Forget(Track t)
    {
        if (t.HasCid && _cidToStable.TryGetValue(t.Cid, out int sid) && sid == t.StableId)
            _cidToStable.Remove(t.Cid);
    }

    /// <summary>给轨道绑定设备报的 CID（CID=0 也算有效身份，只要本帧没有重复）。</summary>
    private void BindCid(Track t, TrackIn c, HashSet<int> dupCids)
    {
        if (!c.HasCid || dupCids.Contains(c.ContactId))
            return;
        t.HasCid = true;
        t.Cid = c.ContactId;
        _cidToStable[c.ContactId] = t.StableId;
    }

    private int NextStableId() => _nextId++;

    private static void Seed(Track t, TrackIn c)
    {
        t.X = c.X;
        t.Y = c.Y;
        t.Pressure01 = c.Pressure01;
        t.Ws.Enqueue(c.W);
        t.Hs.Enqueue(c.H);
    }

    /// <summary>位置原样采用（绝不滤波）；宽高进中值窗口。</summary>
    private static void TrackSample(Track t, TrackIn c)
    {
        t.X = c.X;
        t.Y = c.Y;
        t.Pressure01 = c.Pressure01;
        Push(t.Ws, c.W);
        Push(t.Hs, c.H);
    }

    private static void Push(Queue<float> q, float v)
    {
        q.Enqueue(v);
        while (q.Count > MedianWindow)
            q.Dequeue();
    }

    private static float Median(Queue<float> q)
    {
        if (q.Count == 0)
            return 0f;
        float[] a = q.ToArray();
        Array.Sort(a);
        return a[a.Length / 2];
    }

    private static ContactEvent Event(Track t, Kind kind) =>
        new(t.StableId, kind, t.X, t.Y, Median(t.Ws), Median(t.Hs), t.Pressure01);

    /// <summary>按身份优先级找轨道：CID 可用时直接用 CID（0 也算）；否则位置最近邻（同槽位视为弱证据，优先）。</summary>
    private Track? Match(TrackIn c, HashSet<int> taken, HashSet<int> dupCids, bool relaxed = false)
    {
        long now = Environment.TickCount64;

        // 静默太久的轨道一律不再匹配（它就是"上一笔"，不是这根手指）。
        // 抬手补位（relaxed）例外：那一帧本来就是紧跟最后一次按压的。
        bool usable(Track t) =>
            !taken.Contains(t.StableId)
            && (relaxed || now - t.LastSeenMs <= StaleMatchMs);

        // 设备报了 CID → 用 CID 做真身份（_byStable 的键是自增 StableId，所以要经 _cidToStable 转一道）
        if (c.HasCid && !dupCids.Contains(c.ContactId)
            && _cidToStable.TryGetValue(c.ContactId, out int sid)
            && _byStable.TryGetValue(sid, out Track? byCid)
            && byCid.HasCid && byCid.Cid == c.ContactId
            && usable(byCid))
        {
            return byCid;
        }

        float radius = Radius * (relaxed ? 1.6f : 1f);
        Track? best = null;
        float bestScore = float.MaxValue;
        foreach (KeyValuePair<int, Track> kv in _byStable)
        {
            Track t = kv.Value;
            if (!usable(t))
                continue;
            float dx = t.X - c.X, dy = t.Y - c.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > radius)
                continue;
            // 槽位只做**弱**证据（同槽位略优先），绝不能压过距离：
            // 6 通道 7 指的屏会把第 7 根手指轮流塞进某个槽位，绝对化的槽位优先会让它
            // 抢走该槽原来那根手指的轨道 —— 表现为两根手指被连成一条线、画出实心图形。
            float score = dist * (t.Transport == c.Transport ? 0.9f : 1f);
            if (score < bestScore)
            {
                bestScore = score;
                best = t;
            }
        }
        return best;
    }
}