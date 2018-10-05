using System;
using System.Buffers;
using System.IO;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

namespace AI4E.Memory.Compatibility
{
    public static class StreamExtension
    {
        private static readonly Func<Stream, Memory<byte>, CancellationToken, ValueTask<int>> _readAsyncShim;
        private static readonly Func<Stream, ReadOnlyMemory<byte>, CancellationToken, ValueTask> _writeAsyncShim;

        static StreamExtension()
        {
            var streamType = typeof(Stream);
            var readAsyncMethod = streamType.GetMethod(nameof(Stream.ReadAsync), new[] { typeof(Memory<byte>), typeof(CancellationToken) });

            if (readAsyncMethod != null)
            {
                Assert(readAsyncMethod.ReturnType == typeof(ValueTask<int>));

                var streamParameter = Expression.Parameter(typeof(Stream), "stream");
                var bufferParameter = Expression.Parameter(typeof(Memory<byte>), "buffer");
                var cancellationTokenParameter = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
                var methodCall = Expression.Call(streamParameter, readAsyncMethod);
                _readAsyncShim = Expression.Lambda<Func<Stream, Memory<byte>, CancellationToken, ValueTask<int>>>(
                    methodCall,
                    streamParameter,
                    bufferParameter,
                    cancellationTokenParameter).Compile();
            }

            var writeAsyncMethod = streamType.GetMethod(nameof(Stream.WriteAsync), new[] { typeof(ReadOnlyMemory<byte>), typeof(CancellationToken) });

            if (writeAsyncMethod != null)
            {
                Assert(writeAsyncMethod.ReturnType == typeof(ValueTask));

                var streamParameter = Expression.Parameter(typeof(Stream), "stream");
                var bufferParameter = Expression.Parameter(typeof(ReadOnlyMemory<byte>), "buffer");
                var cancellationTokenParameter = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
                var methodCall = Expression.Call(streamParameter, writeAsyncMethod);
                _writeAsyncShim = Expression.Lambda<Func<Stream, ReadOnlyMemory<byte>, CancellationToken, ValueTask>>(
                    methodCall,
                    streamParameter,
                    bufferParameter,
                    cancellationTokenParameter).Compile();
            }
        }

        public static ValueTask<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (_readAsyncShim != null)
            {
                return _readAsyncShim(stream, buffer, cancellationToken);
            }

            if (stream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var memoryStreamBuffer))
            {
                var position = checked((int)stream.Position);
                var length = checked((int)stream.Length);
                var result = Math.Min(length - position, buffer.Length);

                memoryStreamBuffer.AsMemory().Slice(start: position, length: result).CopyTo(buffer);

                return new ValueTask<int>(result);
            }

            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out var arraySegment))
            {
                return new ValueTask<int>(stream.ReadAsync(arraySegment.Array, arraySegment.Offset, arraySegment.Count));
            }

            return ReadCoreAsync(stream, buffer, cancellationToken);
        }

        private static async ValueTask<int> ReadCoreAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var array = ArrayPool<byte>.Shared.Rent(buffer.Length);

            try
            {
                var result = await stream.ReadAsync(array, offset: 0, buffer.Length, cancellationToken);
                if (result > 0)
                {
                    array.AsMemory().Slice(start: 0, length: result).CopyTo(buffer);
                }
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }

        public static ValueTask WriteAsync(this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (_writeAsyncShim != null)
            {
                return _writeAsyncShim(stream, buffer, cancellationToken);
            }

            if (stream is MemoryStream memoryStream && memoryStream.CanWrite && memoryStream.TryGetBuffer(out var memoryStreamBuffer))
            {
                var position = checked((int)stream.Position);
                var length = checked((int)stream.Length);

                // Check if there is enough space in the stream.
                if (length - position >= buffer.Length)
                {
                    buffer.CopyTo(memoryStreamBuffer.AsMemory().Slice(start: position));
                    return new ValueTask(Task.CompletedTask); // TODO: How can we return an already completed ValueTask??
                }
            }

            if (MemoryMarshal.TryGetArray(buffer, out var arraySegment))
            {
                return new ValueTask(stream.WriteAsync(arraySegment.Array, arraySegment.Offset, arraySegment.Count));
            }

            return WriteCoreAsync(stream, buffer, cancellationToken);
        }

        private static async ValueTask WriteCoreAsync(Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            var array = ArrayPool<byte>.Shared.Rent(buffer.Length);

            try
            {
                buffer.CopyTo(array);

                await stream.WriteAsync(array, offset: 0, buffer.Length, cancellationToken);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }
    }
}
