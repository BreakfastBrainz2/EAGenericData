using System;

namespace EAGenericData.Layout
{
    [Flags]
    public enum ReflFieldCategory
    {
        Invalid = 0,
        Value = 1 << 0,
        Nested = 1 << 1,
        Array = 1 << 2,
        List = 1 << 3,

        ArrayValue = Array | Value,
        ArrayNested = Array | Nested,
        ListValue = List | Value,
        ListNested = List | Nested,
        ArrayListValue = Array | List | Value,
        ArrayListNested = Array | List | Nested
    }
}