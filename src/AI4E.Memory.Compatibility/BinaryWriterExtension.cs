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
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using static System.Diagnostics.Debug;

namespace System.IO
{
    public static class BinaryWriterExtension
    {
        private static readonly WriteBytesShim _writeBytesShim;
        private static readonly WriteCharsShim _writeCharsShim;

        static BinaryWriterExtension()
        {
            var binaryWriterType = typeof(BinaryWriter);

            _writeBytesShim = BuildWriteBytesShim(binaryWriterType);
            _writeCharsShim = BuildWriteCharsShim(binaryWriterType);
        }

        private static WriteBytesShim BuildWriteBytesShim(Type binaryWriterType)
        {
            var writeMethod = binaryWriterType.GetMethod(nameof(Write), new[] { typeof(ReadOnlySpan<byte>) });

            if (writeMethod == null)
                return null;

            Assert(writeMethod.ReturnType == typeof(void));

            var binaryWriterParameter = Expression.Parameter(binaryWriterType, "writer");
            var bufferParameter = Expression.Parameter(typeof(ReadOnlySpan<byte>), "buffer");
            var methodCall = Expression.Call(binaryWriterParameter, writeMethod, bufferParameter);
            return Expression.Lambda<WriteBytesShim>(methodCall, binaryWriterParameter, bufferParameter).Compile();
        }

        private static WriteCharsShim BuildWriteCharsShim(Type binaryWriterType)
        {
            var writeMethod = binaryWriterType.GetMethod(nameof(Write), new[] { typeof(ReadOnlySpan<char>) });

            if (writeMethod == null)
                return null;

            Assert(writeMethod.ReturnType == typeof(void));

            var binaryWriterParameter = Expression.Parameter(binaryWriterType, "writer");
            var charsParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "chars");
            var methodCall = Expression.Call(binaryWriterParameter, writeMethod, charsParameter);
            return Expression.Lambda<WriteCharsShim>(methodCall, binaryWriterParameter, charsParameter).Compile();
        }

        private delegate void WriteBytesShim(BinaryWriter writer, ReadOnlySpan<byte> buffer);
        private delegate void WriteCharsShim(BinaryWriter writer, ReadOnlySpan<char> chars);

        public static void Write(this BinaryWriter writer, ReadOnlySpan<byte> buffer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            if (_writeBytesShim != null)
            {
                _writeBytesShim(writer, buffer);
                return;
            }

            writer.Flush();

            var underlyingStream = writer.BaseStream;
            Assert(underlyingStream != null);

            underlyingStream.Write(buffer);
        }

        public static void Write(this BinaryWriter writer, ReadOnlySpan<char> chars)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            if (_writeCharsShim != null)
            {
                _writeCharsShim(writer, chars);
                return;
            }

            var encoding = TryGetEncoding(writer);

            if (encoding != null)
            {
                var array = ArrayPool<byte>.Shared.Rent(encoding.GetByteCount(chars));
                try
                {
                    var byteCount = encoding.GetBytes(chars, array);

                    writer.Write7BitEncodedInt(byteCount);
                    writer.Write(array, index: 0, byteCount);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(array);
                }
            }

            var str = new string('\0', chars.Length);
            chars.CopyTo(MemoryMarshal.AsMemory(str.AsMemory()).Span);

            writer.Write(str);
        }

        private static readonly Lazy<Func<BinaryWriter, Encoding>> _encodingLookupLazy
            = new Lazy<Func<BinaryWriter, Encoding>>(BuildEncodingLookup, LazyThreadSafetyMode.PublicationOnly);

        private static Encoding TryGetEncoding(BinaryWriter writer)
        {
            return _encodingLookupLazy.Value(writer);
        }

        private static Func<BinaryWriter, Encoding> BuildEncodingLookup()
        {
            var binaryWriterType = typeof(BinaryWriter);
            var encodingType = typeof(Encoding);

            var encodingField = binaryWriterType.GetField("_encoding", BindingFlags.Instance | BindingFlags.NonPublic);

            // corefx
            if (encodingField != null && encodingField.FieldType == encodingType)
            {
                var binaryWriterParameter = Expression.Parameter(binaryWriterType, "writer");
                var fieldAccess = Expression.MakeMemberAccess(binaryWriterParameter, encodingField);
                return Expression.Lambda<Func<BinaryWriter, Encoding>>(fieldAccess, binaryWriterParameter).Compile();
            }

            var decoderType = typeof(Decoder);
            var decoderField = binaryWriterType.GetField("m_decoder", BindingFlags.Instance | BindingFlags.NonPublic);

            // .Net Framework
            if (decoderField != null && decoderField.FieldType == decoderType)
            {
                var defaultDecoderType = Type.GetType("System.Text.Encoding.DefaultDecoder, mscorlib", throwOnError: false);

                if (defaultDecoderType == null)
                    return _ => null;

                encodingField = defaultDecoderType.GetField("m_encoding", BindingFlags.Instance | BindingFlags.NonPublic);

                if (encodingField == null || encodingField.FieldType != encodingType)
                    return _ => null;

                var binaryWriterParameter = Expression.Parameter(binaryWriterType, "writer");
                var decoderFieldAccess = Expression.MakeMemberAccess(binaryWriterParameter, decoderField);
                var isDefaultDecoder = Expression.TypeIs(decoderFieldAccess, defaultDecoderType);


                var decoderConvert = Expression.Convert(decoderFieldAccess, defaultDecoderType);
                var encodingFieldAccess = Expression.MakeMemberAccess(decoderConvert, encodingField);

                var nullConstant = Expression.Constant(null, typeof(Encoding));
                var result = Expression.Condition(isDefaultDecoder, encodingFieldAccess, nullConstant);

                return Expression.Lambda<Func<BinaryWriter, Encoding>>(result, binaryWriterParameter).Compile();
            }

            return _ => null;
        }

        private static void Write7BitEncodedInt(this BinaryWriter writer, int value)
        {
            var v = (uint)value;
            while (v >= 0x80)
            {
                writer.Write((byte)(v | 0x80));
                v >>= 7;
            }
            writer.Write((byte)v);
        }
    }
}
