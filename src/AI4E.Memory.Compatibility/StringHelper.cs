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
