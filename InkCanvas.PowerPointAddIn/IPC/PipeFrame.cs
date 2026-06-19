using System;
using System.IO;
using System.Text;
using InkCanvasPPTAgent.Contracts;

namespace InkCanvas.PowerPointAddIn.IPC
{
    public static class PipeFrame
    {
        public static void WriteFrame(Stream stream, string json)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (json == null) throw new ArgumentNullException(nameof(json));

            byte[] data = Encoding.UTF8.GetBytes(json);
            if (data.Length <= 0 || data.Length > PipeConstants.MaxFrameSize)
                throw new InvalidOperationException("Invalid frame size.");

            byte[] lenBytes = BitConverter.GetBytes(data.Length);
            stream.Write(lenBytes, 0, lenBytes.Length);
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }

        public static string ReadFrame(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            byte[] lenBytes = ReadExact(stream, 4);
            int len = BitConverter.ToInt32(lenBytes, 0);
            if (len <= 0 || len > PipeConstants.MaxFrameSize)
                throw new InvalidOperationException("Invalid frame size.");

            byte[] data = ReadExact(stream, len);
            return Encoding.UTF8.GetString(data);
        }

        private static byte[] ReadExact(Stream stream, int size)
        {
            byte[] buffer = new byte[size];
            int offset = 0;

            while (offset < size)
            {
                int read = stream.Read(buffer, offset, size - offset);
                if (read <= 0)
                    throw new IOException("Pipe closed.");
                offset += read;
            }

            return buffer;
        }
    }
}
