using System;

namespace EAGenericData.Layout
{
    // Used to manually define field entries in a layout. See ReflLayoutType for examples.
    public struct ReflLayoutField
    {
        public readonly int Id;
        public readonly string Name;
        public readonly ReflLayout Layout;
        public readonly ReflLayoutFlags Flags;
        public readonly int Count;

        public ReflLayoutField(
            int id,
            string name,
            ReflLayout layout,
            ReflLayoutFlags flags = ReflLayoutFlags.None,
            int count = 1)
        {
            Id = id;
            Name = name;
            Layout = layout;
            Flags = flags;
            Count = Math.Max(1, count);
        }
    }
}