using System;

namespace AI4E.Memory.Compatibility
{
    internal static class RuntimeHelper
    {
        // Adapted from: https://stackoverflow.com/questions/721161/how-to-detect-which-net-runtime-is-being-used-ms-vs-mono#721194
        public static bool IsRunningOnMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }
    }
}
