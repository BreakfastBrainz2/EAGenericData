using System;
using System.IO;
using System.Text;

namespace EAGenericData.IO
{
    public class ExtendedBinaryReader : BinaryReader
    {
        public Endian Endianness { get; set; } = Endian.Little;

        public long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }
        
        public ExtendedBinaryReader(Stream stream) : base(stream)
        {}

        public override short ReadInt16()
        {
            if (Endianness != Endian.Little)
                return DataUtil.SwapEndian(base.ReadInt16());

            return base.ReadInt16();
        }

        public override ushort ReadUInt16()
        {
            if (Endianness != Endian.Little)
                return DataUtil.SwapEndian(base.ReadUInt16());

            return base.ReadUInt16();
        }

        public override int ReadInt32()
        {
            if (Endianness != Endian.Little)
                return DataUtil.SwapEndian(base.ReadInt32());

            return base.ReadInt32();
        }

        public override uint ReadUInt32()
        {
            if (Endianness != Endian.Little)
                return DataUtil.SwapEndian(base.ReadUInt32());

            return base.ReadUInt32();
        }

        public override long ReadInt64()
        {
            if (Endianness != Endian.Little)
                return DataUtil.SwapEndian(base.ReadInt64());

            return base.ReadInt64();
        }

        public override ulong ReadUInt64()
        {
            if (Endianness != Endian.Little)
                return DataUtil.SwapEndian(base.ReadUInt64());

            return base.ReadUInt64();
        }

        public override float ReadSingle()
        {
            if (Endianness != Endian.Little)
            {
                FloatUnion union = new FloatUnion { Value = ReadUInt32() };
                return union.Float;
            }

            return base.ReadSingle();
        }

        public override double ReadDouble()
        {
            if (Endianness != Endian.Little)
            {
                DoubleUnion union = new DoubleUnion { Value = ReadUInt64() };
                return union.Double;
            }

            return base.ReadDouble();
        }

        public int ReadInt32(Endian endian)
        {
            if (endian != Endian.Little)
                return DataUtil.SwapEndian(base.ReadInt32());

            return base.ReadInt32();
        }
        
        public uint ReadUInt32(Endian endian)
        {
            if (endian != Endian.Little)
                return DataUtil.SwapEndian(base.ReadUInt32());

            return base.ReadUInt32();
        }

        public string ReadNullTerminatedString()
        {
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                char c = (char)ReadByte();
                if (c == '\0')
                    return sb.ToString();

                sb.Append(c);
            }
        }
        
        public Guid ReadGuid()
        {
            byte[] buffer = ReadBytes(16);
            if (Endianness == Endian.Little)
                return new Guid(new byte[] {
                    buffer[0], buffer[1], buffer[2], buffer[3], buffer[4], buffer[5], buffer[6], buffer[7],
                    buffer[8], buffer[9], buffer[10], buffer[11], buffer[12], buffer[13], buffer[14], buffer[15]
                });

            return new Guid(new byte[] {
                buffer[3], buffer[2], buffer[1], buffer[0], buffer[5], buffer[4], buffer[7], buffer[6],
                buffer[8], buffer[9], buffer[10], buffer[11], buffer[12], buffer[13], buffer[14], buffer[15]
            });
        }
    }
}