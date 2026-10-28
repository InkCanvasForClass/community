using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InkCanvasPPTAgent.Contracts
{
    public static class PipeFrame
    {
        public static async Task WriteFrameAsync(Stream stream, string json, CancellationToken cancellationToken)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (json == null) throw new ArgumentNullException(nameof(json));
            var data = Encoding.UTF8.GetBytes(json);
            if (data.Length <= 0 || data.Length > PipeConstants.MaxFrameSize)
                throw new InvalidOperationException("Invalid frame size.");
            var length = BitConverter.GetBytes(data.Length);
            await stream.WriteAsync(length, 0, length.Length, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public static void WriteFrame(Stream stream, string json)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (json == null) throw new ArgumentNullException(nameof(json));

            var data = Encoding.UTF8.GetBytes(json);
            if (data.Length <= 0 || data.Length > PipeConstants.MaxFrameSize)
                throw new InvalidOperationException("Invalid frame size.");

            var lenBytes = BitConverter.GetBytes(data.Length);
            stream.Write(lenBytes, 0, lenBytes.Length);
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }

        public static string ReadFrame(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var lenBytes = ReadExact(stream, 4);
            var len = BitConverter.ToInt32(lenBytes, 0);
            if (len <= 0 || len > PipeConstants.MaxFrameSize)
                throw new InvalidOperationException("Invalid frame size.");

            var data = ReadExact(stream, len);
            return Encoding.UTF8.GetString(data);
        }

        private static byte[] ReadExact(Stream stream, int size)
        {
            var buffer = new byte[size];
            var offset = 0;

            while (offset < size)
            {
                var read = stream.Read(buffer, offset, size - offset);
                if (read <= 0)
                    throw new IOException("Pipe closed.");

                offset += read;
            }

            return buffer;
        }
    }
}
