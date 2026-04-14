using System.Runtime.InteropServices;

namespace EAGenericData
{
    [StructLayout(LayoutKind.Explicit)]
    public struct FloatUnion
    {
        [FieldOffset(0)]
        public float Float;
        [FieldOffset(0)]
        public uint Value;
    }
        
    [StructLayout(LayoutKind.Explicit)]
    public struct DoubleUnion
    {
        [FieldOffset(0)]
        public double Double;
        [FieldOffset(0)]
        public ulong Value;
    }
    
    public static class DataUtil
    {
        // you're fucked if this ends up in a file
        public const uint PTR_PLACEHOLDER = 0xDEADBEEF;
        public const long PTR_PLACEHOLDER_LONG = 0xDEADBEEF0000;

        public const int GD_ARRAY_SIZE = 16;
        public const int GD_ARRAY_ALIGN = 8;
        
        public static int Align(int value, int align)
        {
            return (value + (align - 1)) & ~(align - 1);
        }
        
        public static long Align(long value, long align)
        {
            return (value + (align - 1)) & ~(align - 1);
        }
        
        public static int ArraySize(int size, int align, int count)
        {
            if (count == 0)
            {
                return 0;
            }
            return Align(size, align) * (count - 1) + size;
        }
        
        public static ushort SwapEndian(ushort value)
        {
            return (ushort)((value >> 8) | (value << 8));
        }

        public static short SwapEndian(short value)
        {
            return (short)SwapEndian((ushort)value);
        }
        
        public static uint SwapEndian(uint value)
        {
            return
                ((value & 0x000000FF) << 24) |
                ((value & 0x0000FF00) << 8)  |
                ((value & 0x00FF0000) >> 8)  |
                ((value & 0xFF000000) >> 24);
        }
        
        public static int SwapEndian(int value)
        {
            return (int)SwapEndian((uint)value);
        }

        public static ulong SwapEndian(ulong value)
        {
            return (0xFF & (value >> 56)) |
                   (0xFF00 & (value >> 40)) |
                   (0xFF0000 & (value >> 24)) |
                   (0xFF000000u & (value >> 8)) |
                   (0xFF00000000L & (value << 8)) |
                   (0xFF0000000000L & (value << 24)) |
                   (0xFF000000000000L & (value << 40)) |
                   (0xFF00000000000000uL & (value << 56));
        }

        public static long SwapEndian(long value)
        {
            return (long)SwapEndian((ulong)value);
        }
    }
}