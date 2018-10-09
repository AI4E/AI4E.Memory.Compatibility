using System;
using System.Text;

namespace AI4E.Memory.Compatibility
{
    public static class EncodingExtension
    {
        public static unsafe int GetByteCount(this Encoding encoding, ReadOnlySpan<char> chars)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            fixed (char* charsPtr = chars)
            {
                return encoding.GetByteCount(charsPtr, chars.Length);
            }
        }

        public static unsafe int GetBytes(this Encoding encoding, ReadOnlySpan<char> chars, Span<byte> bytes)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            fixed (char* charsPtr = chars)
            fixed (byte* bytesPtr = bytes)
            {
                return encoding.GetBytes(charsPtr, chars.Length, bytesPtr, bytes.Length);
            }
        }

        public static unsafe int GetCharCount(this Encoding encoding, ReadOnlySpan<byte> bytes)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            fixed (byte* bytesPtr = bytes)
            {
                return encoding.GetCharCount(bytesPtr, bytes.Length);
            }
        }

        public static unsafe int GetChars(this Encoding encoding, ReadOnlySpan<byte> bytes, Span<char> chars)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            fixed (byte* bytesPtr = bytes)
            fixed (char* charsPtr = chars)
            {
                return encoding.GetChars(bytesPtr, bytes.Length, charsPtr, chars.Length);
            }
        }

        public static unsafe string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            fixed (byte* bytesPtr = bytes)
            {
                return encoding.GetString(bytesPtr, bytes.Length);
            }
        }
    }
}
