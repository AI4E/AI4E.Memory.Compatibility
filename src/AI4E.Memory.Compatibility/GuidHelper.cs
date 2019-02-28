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

using System;
using System.Linq.Expressions;
using System.Reflection;
using static System.Diagnostics.Debug;

namespace AI4E.Memory.Compatibility
{
    public static class GuidHelper
    {
        private static readonly CreateGuidShim _createGuidShim;
        private static readonly ParseShim _parseShim;
        private static readonly ParseExectShim _parseExectShim;
        private static readonly TryFormatShim _tryFormatShim;
        private static readonly TryParseShim _tryParseShim;
        private static readonly TryParseExactShim _tryParseExactShim;
        private static readonly TryWriteBytesShim _tryWriteBytesShim;

        static GuidHelper()
        {
            var guidType = typeof(Guid);

            if (guidType != null)
            {
                _createGuidShim = BuildCreateGuidShim(guidType);
                _parseShim = BuildParseShim(guidType);
                _parseExectShim = BuildParseExactShim(guidType);
                _tryFormatShim = BuildTryFormatShim(guidType);
                _tryParseShim = BuildTryParseShim(guidType);
                _tryParseExactShim = BuildTryParseExactShim(guidType);
                _tryWriteBytesShim = BuildTryWriteBytesShim(guidType);
            }
        }

        private static CreateGuidShim BuildCreateGuidShim(Type guidType)
        {
            var ctor = guidType.GetConstructor(BindingFlags.Public | BindingFlags.Instance,
                                               Type.DefaultBinder,
                                               new Type[] { typeof(ReadOnlySpan<byte>) },
                                               modifiers: null);

            if (ctor == null)
            {
                return null;
            }

            var bParameter = Expression.Parameter(typeof(ReadOnlySpan<byte>), "b");
            var call = Expression.New(ctor, bParameter);
            var lambda = Expression.Lambda<CreateGuidShim>(call, bParameter);
            return lambda.Compile();
        }

        private static ParseShim BuildParseShim(Type guidType)
        {
            var parseMethod = guidType.GetMethod(nameof(Guid.Parse),
                                                 BindingFlags.Public | BindingFlags.Static,
                                                 Type.DefaultBinder,
                                                 new Type[] { typeof(ReadOnlySpan<char>) },
                                                 modifiers: null);

            if (parseMethod == null)
            {
                return null;
            }

            Assert(parseMethod.ReturnType == typeof(Guid));

            var inputParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "input");
            var call = Expression.Call(parseMethod, inputParameter);
            var lambda = Expression.Lambda<ParseShim>(call, inputParameter);
            return lambda.Compile();
        }

        private static ParseExectShim BuildParseExactShim(Type guidType)
        {
            var parseExactMethod = guidType.GetMethod(nameof(Guid.ParseExact),
                                                 BindingFlags.Public | BindingFlags.Static,
                                                 Type.DefaultBinder,
                                                 new Type[] { typeof(ReadOnlySpan<char>), typeof(ReadOnlySpan<char>) },
                                                 modifiers: null);

            if (parseExactMethod == null)
            {
                return null;
            }

            Assert(parseExactMethod.ReturnType == typeof(Guid));

            var inputParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "input");
            var formatParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "format");
            var call = Expression.Call(parseExactMethod, inputParameter, formatParameter);
            var lambda = Expression.Lambda<ParseExectShim>(call, inputParameter, formatParameter);
            return lambda.Compile();
        }

        private static TryFormatShim BuildTryFormatShim(Type guidType)
        {
            var tryFormatMethod = guidType.GetMethod("TryFormat",
                                                     BindingFlags.Public | BindingFlags.Instance,
                                                     Type.DefaultBinder,
                                                     new Type[] { typeof(Span<char>), typeof(int).MakeByRefType(), typeof(ReadOnlySpan<char>) },
                                                     modifiers: null);

            if (tryFormatMethod == null)
            {
                return null;
            }

            Assert(tryFormatMethod.ReturnType == typeof(bool));

            var guidParameter = Expression.Parameter(typeof(Guid), "guid");
            var destinationParameter = Expression.Parameter(typeof(Span<char>), "destination");
            var charsWrittenParameter = Expression.Parameter(typeof(int).MakeByRefType(), "charsWritten");
            var formatParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "format");

            var call = Expression.Call(guidParameter, tryFormatMethod, destinationParameter, charsWrittenParameter, formatParameter);
            var lambda = Expression.Lambda<TryFormatShim>(call, guidParameter, destinationParameter, charsWrittenParameter, formatParameter);
            return lambda.Compile();
        }

        private static TryParseShim BuildTryParseShim(Type guidType)
        {
            var tryParseMethod = guidType.GetMethod(nameof(Guid.TryParse),
                                                 BindingFlags.Public | BindingFlags.Static,
                                                 Type.DefaultBinder,
                                                 new Type[] { typeof(ReadOnlySpan<char>), typeof(Guid).MakeByRefType() },
                                                 modifiers: null);

            if (tryParseMethod == null)
            {
                return null;
            }

            Assert(tryParseMethod.ReturnType == typeof(bool));

            var inputParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "input");
            var resultParameter = Expression.Parameter(typeof(Guid).MakeByRefType(), "result");
            var call = Expression.Call(tryParseMethod, inputParameter, resultParameter);
            var lambda = Expression.Lambda<TryParseShim>(call, inputParameter, resultParameter);
            return lambda.Compile();
        }

        private static TryParseExactShim BuildTryParseExactShim(Type guidType)
        {
            var tryParseExactMethod = guidType.GetMethod(nameof(Guid.TryParseExact),
                                                  BindingFlags.Public | BindingFlags.Static,
                                                  Type.DefaultBinder,
                                                  new Type[] { typeof(ReadOnlySpan<char>), typeof(ReadOnlySpan<char>), typeof(Guid).MakeByRefType() },
                                                  modifiers: null);

            if (tryParseExactMethod == null)
            {
                return null;
            }

            Assert(tryParseExactMethod.ReturnType == typeof(bool));

            var inputParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "input");
            var formatParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "format");
            var resultParameter = Expression.Parameter(typeof(Guid).MakeByRefType(), "result");
            var call = Expression.Call(tryParseExactMethod, inputParameter, formatParameter, resultParameter);
            var lambda = Expression.Lambda<TryParseExactShim>(call, inputParameter, formatParameter, resultParameter);
            return lambda.Compile();
        }

        private static TryWriteBytesShim BuildTryWriteBytesShim(Type guidType)
        {
            var tryWriteBytesMethod = guidType.GetMethod("TryWriteBytes",
                                                         BindingFlags.Public | BindingFlags.Instance,
                                                         Type.DefaultBinder,
                                                         new Type[] { typeof(Span<byte>) },
                                                         modifiers: null);

            if (tryWriteBytesMethod == null)
            {
                return null;
            }

            Assert(tryWriteBytesMethod.ReturnType == typeof(bool));

            var guidParameter = Expression.Parameter(typeof(Guid), "guid");
            var destinationParameter = Expression.Parameter(typeof(Span<byte>), "destination");
  
            var call = Expression.Call(guidParameter, tryWriteBytesMethod, destinationParameter);
            var lambda = Expression.Lambda<TryWriteBytesShim>(call, guidParameter, destinationParameter);
            return lambda.Compile();
        }

        private delegate Guid CreateGuidShim(ReadOnlySpan<byte> b);
        private delegate Guid ParseShim(ReadOnlySpan<char> input);
        private delegate Guid ParseExectShim(ReadOnlySpan<char> input, ReadOnlySpan<char> format);
        private delegate bool TryFormatShim(Guid guid, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format);
        private delegate bool TryParseShim(ReadOnlySpan<char> input, out Guid result);
        private delegate bool TryParseExactShim(ReadOnlySpan<char> input, ReadOnlySpan<char> format, out Guid result);
        private delegate bool TryWriteBytesShim(Guid guid, Span<byte> destination);

        public static Guid CreateGuid(ReadOnlySpan<byte> b)
        {
            if (_createGuidShim != null)
            {
                return _createGuidShim(b);
            }

            if ((uint)b.Length != 16)
            {
                throw new ArgumentException("The span must be 16 bytes long.", nameof(b));
            }

            // Adapted from: https://github.com/dotnet/corefx/blob/b51c5b8bed06f924b5470d9042d4de0381dd89c9/src/Common/src/CoreLib/System/Guid.cs#L54
            var x = b[3] << 24 | b[2] << 16 | b[1] << 8 | b[0];
            var y = (short)(b[5] << 8 | b[4]);
            var z = (short)(b[7] << 8 | b[6]);

            return new Guid(
                x, y, z,
                b[8], b[9], b[10],
                b[11], b[12], b[13],
                b[14], b[15]);
        }

        public static Guid Parse(ReadOnlySpan<char> input)
        {
            if (_parseShim != null)
            {
                return _parseShim(input);
            }

            var stringInput = StringHelper.Create(input);

            return Guid.Parse(stringInput);
        }

        public static Guid ParseExact(ReadOnlySpan<char> input, ReadOnlySpan<char> format)
        {
            if (_parseExectShim != null)
            {
                return _parseExectShim(input, format);
            }

            var stringInput = StringHelper.Create(input);
            var stringFormat = StringHelper.Create(format);

            return Guid.ParseExact(stringInput, stringFormat);
        }

        public static bool TryFormat(in this Guid guid, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default)
        {
            if (_tryFormatShim != null)
            {
                return _tryFormatShim(guid, destination, out charsWritten, format);
            }

            var stringFormat = (format.IsEmpty || format.IsWhiteSpace()) ? null : StringHelper.Create(format);
            var result = guid.ToString(stringFormat);

            if (result.Length > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            result.AsSpan().CopyTo(destination.Slice(start: 0, result.Length));

            charsWritten = result.Length;
            return true;
        }

        public static bool TryParse(ReadOnlySpan<char> input, out Guid result)
        {
            if (_tryParseShim != null)
            {
                return _tryParseShim(input, out result);
            }

            var stringInput = StringHelper.Create(input);

            return Guid.TryParse(stringInput, out result);
        }

        public static bool TryParseExact(ReadOnlySpan<char> input, ReadOnlySpan<char> format, out Guid result)
        {
            if (_tryParseExactShim != null)
            {
                return _tryParseExactShim(input, format, out result);
            }

            var stringInput = StringHelper.Create(input);
            var stringFormat = StringHelper.Create(format);

            return Guid.TryParseExact(stringInput, stringFormat, out result);
        }

        public static bool TryWriteBytes(in this Guid guid, Span<byte> destination)
        {
            if (_tryWriteBytesShim != null)
            {
                return _tryWriteBytesShim(guid, destination);
            }

            const int _guidLength = 16;

            if (destination.Length < _guidLength)
                return false;

            unsafe
            {
                fixed (Guid* guidPtr = &guid)
                {
                    var source = new Span<byte>(guidPtr, _guidLength);
                    source.CopyTo(destination.Slice(start: 0, length: _guidLength));
                }
            }

            return true;
        }
    }
}
