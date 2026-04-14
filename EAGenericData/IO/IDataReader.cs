using System;
using System.IO;

namespace EAGenericData.IO
{
    public interface IDataReader
    {
        long Position { get; set; }
        Endian Endianness { get; set; }

        byte[] ReadBytes(int count);
        bool ReadBool();
        sbyte ReadInt8();
        byte ReadUInt8();
        short ReadInt16();
        ushort ReadUInt16();
        int ReadInt32();
        uint ReadUInt32();
        long ReadInt64();
        ulong ReadUInt64();
        float ReadFloat();
        double ReadDouble();
        Guid ReadGuid();

        void Seek(long offset, SeekOrigin origin);
    }
}