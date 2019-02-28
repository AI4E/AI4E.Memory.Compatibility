/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E.Memory.Compatibility)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
 * 
 * MIT License
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System.Buffers;
using System.Linq.Expressions;
using System.Reflection;
using static System.Diagnostics.Debug;

namespace System
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
