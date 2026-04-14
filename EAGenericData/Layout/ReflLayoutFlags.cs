using System;

namespace EAGenericData.Layout
{
    [Flags]
    public enum ReflLayoutFlags
    {
        None = 0,
        Array = 1 << 0,
        ForceAlign4 = 1 << 1,
        ForceAlign8 = 1 << 2,
        ForceAlign16 = 1 << 3
    }
}