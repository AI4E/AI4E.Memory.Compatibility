using System;
using System.Buffers;
using System.Linq.Expressions;
using System.Reflection;
using static System.Diagnostics.Debug;

namespace AI4E.Memory.Compatibility
{
    public static class RandomExtension
    {
        private static readonly NextBytesShim _nextBytesShim;

        static RandomExtension()
        {
            var randomType = typeof(Random);

            if (randomType != null)
            {
                _nextBytesShim = BuildNextBytesShim(randomType);
            }
        }

        private static NextBytesShim BuildNextBytesShim(Type randomType)
        {
            var nextBytesMethod = randomType.GetMethod(nameof(Random.NextBytes),
                                                       BindingFlags.Instance | BindingFlags.Public,
                                                       Type.DefaultBinder,
                                                       new Type[] { typeof(Span<byte>) },
                                                       modifiers: null);

            if (nextBytesMethod == null)
                return null;

            Assert(nextBytesMethod.ReturnType == typeof(void));

            var randomParameter = Expression.Parameter(typeof(Random), "random");
            var bufferParameter = Expression.Parameter(typeof(Span<byte>), "buffer");
            var call = Expression.Call(randomParameter, nextBytesMethod, bufferParameter);
            var lambda = Expression.Lambda<NextBytesShim>(call, randomParameter, bufferParameter);

            return lambda.Compile();
        }

        private delegate void NextBytesShim(Random random, Span<byte> buffer);

        public static void NextBytes(this Random random, Span<byte> buffer)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            if (_nextBytesShim != null)
            {
                _nextBytesShim(random, buffer);
                return;
            }

            var array = ArrayPool<byte>.Shared.Rent(buffer.Length);

            try
            {
                random.NextBytes(array);

                array.AsSpan(start: 0, length: buffer.Length).CopyTo(buffer);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }
    }
}
