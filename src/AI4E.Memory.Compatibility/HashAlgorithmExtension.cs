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

namespace System.Security.Cryptography
{
    public static class HashAlgorithmExtension
    {
        private static readonly TryComputeHashShim _tryComputeHashShim;

        static HashAlgorithmExtension()
        {
            var hashAlgorithmType = typeof(HashAlgorithm);

            if (hashAlgorithmType != null)
            {
                _tryComputeHashShim = BuildTryComputeHashShim(hashAlgorithmType);
            }
        }

        private static TryComputeHashShim BuildTryComputeHashShim(Type hashAlgorithmType)
        {
            var tryCompateHashMethod = hashAlgorithmType.GetMethod("TryComputeHash",
                                                                   BindingFlags.Instance | BindingFlags.Public,
                                                                   Type.DefaultBinder,
                                                                   new Type[] { typeof(ReadOnlySpan<byte>), typeof(Span<byte>), typeof(int).MakeByRefType() },
                                                                   modifiers: null);

            if (tryCompateHashMethod == null)
                return null;

            Assert(tryCompateHashMethod.ReturnType == typeof(bool));

            var hashAlgorithmParameter = Expression.Parameter(hashAlgorithmType, "hashAlgorithm");
            var sourceParameter = Expression.Parameter(typeof(ReadOnlySpan<byte>), "source");
            var destinationParameter = Expression.Parameter(typeof(Span<byte>), "destination");
            var bytesWrittenParameter = Expression.Parameter(typeof(int).MakeByRefType(), "bytesWritten");
            var call = Expression.Call(hashAlgorithmParameter, tryCompateHashMethod, sourceParameter, destinationParameter, bytesWrittenParameter);
            var lambda = Expression.Lambda<TryComputeHashShim>(call, hashAlgorithmParameter, sourceParameter, destinationParameter, bytesWrittenParameter);
            return lambda.Compile();
        }

        private delegate bool TryComputeHashShim(HashAlgorithm hashAlgorithm, ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten);

        public static bool TryComputeHash(this HashAlgorithm hashAlgorithm, ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten)
        {
            if (hashAlgorithm == null)
                throw new ArgumentNullException(nameof(hashAlgorithm));

            if (_tryComputeHashShim != null)
            {
                return _tryComputeHashShim(hashAlgorithm, source, destination, out bytesWritten);
            }

            byte[] destinationArray;

            var sourceArray = ArrayPool<byte>.Shared.Rent(source.Length);
            try
            {
                source.CopyTo(sourceArray.AsSpan().Slice(start: 0, length: source.Length));

                destinationArray = hashAlgorithm.ComputeHash(sourceArray, offset: 0, count: source.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(sourceArray);
            }

            if (destinationArray.Length > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            destinationArray.AsSpan().CopyTo(destination.Slice(start: 0, length: destinationArray.Length));
            bytesWritten = destinationArray.Length;
            return true;
        }
    }
}
