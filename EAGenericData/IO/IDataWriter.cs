using System;
using System.Numerics;
using EAGenericData.Serialization;

namespace EAGenericData.IO
{
    public interface IDataWriter
    {
        long Position { get; set; }
        long Length { get; }
        Endian Endianness { get; set; }

        void Write(byte[] aData);
        void Write(byte[] aData, int aOffset, int aLength);
        void WriteBool(bool aValue);
        void WriteInt8(sbyte aValue);
        void WriteUInt8(byte aValue);
        void WriteInt16(short aValue);
        void WriteUInt16(ushort aValue);
        void WriteInt32(int aValue);
        void WriteUInt32(uint aValue);
        void WriteInt64(long aValue);
        void WriteUInt64(ulong aValue);
        void WriteFloat(float aValue);
        void WriteDouble(double aValue);
        void WriteGuid(Guid value);
        void WriteVector2(Vector2 value);
        void WriteVector3(Vector3 value);
        void WriteVector4(Vector4 value);
        void WriteQuaternion(Quaternion value);
        void WriteMatrix4x4(Matrix4x4 value);
        void WriteReloc(Relocation aValue);
    }
}