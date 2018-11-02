using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AI4E.Memory.Compatibility
{
    public static class StringHelper
    {
        private static readonly CreateShim _createShim;

        static StringHelper()
        {
            var stringType = typeof(string);

            if (stringType != null)
            {
                _createShim = BuildCreateShim(stringType);
            }
        }

        private static CreateShim BuildCreateShim(Type stringType)
        {
            var ctor = stringType.GetConstructor(BindingFlags.Instance | BindingFlags.Public,
                                                 Type.DefaultBinder,
                                                 new Type[] { typeof(ReadOnlySpan<char>) },
                                                 modifiers: null);

            if (ctor == null)
                return null;

            var valueParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "value");
            var call = Expression.New(ctor, valueParameter);
            var lambda = Expression.Lambda<CreateShim>(call, valueParameter);
            return lambda.Compile();
        }

        private delegate string CreateShim(ReadOnlySpan<char> value);

        public static string Create(ReadOnlySpan<char> value)
        {
            if (_createShim != null)
            {
                return _createShim(value);
            }

            var result = new string('\0', value.Length);
            var dest = MemoryMarshal.AsMemory(result.AsMemory()).Span;

            value.CopyTo(dest);

            return result;
        }
    }
}
