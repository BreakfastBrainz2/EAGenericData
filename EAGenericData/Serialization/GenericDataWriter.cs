using System;
using System.Numerics;
using System.Text;
using EAGenericData.IO;
using EAGenericData.Layout;

namespace EAGenericData.Serialization
{
    public class GenericDataWriter : IDataWriter
    {
        private readonly GenericDataBlobWriter m_blobWriter;
        private readonly RelocationTable m_relocTable;

        public long Position
        {
            get => m_blobWriter.Position;
            set => m_blobWriter.Position = value;
        }

        public long Length
        {
            get => m_blobWriter.Length;
            set => m_blobWriter.Length = value;
        }

        public Endian Endianness
        {
            get => m_blobWriter.Endianness;
            set => m_blobWriter.Endianness = value;
        }

        public GenericDataWriter(GenericDataBlobWriter writer, RelocationTable table)
        {
            m_blobWriter = writer;
            m_relocTable = table;
        }

        public void Write(byte[] aData) => m_blobWriter.Write(aData);
        public void Write(byte[] aData, int aOffset, int aLength) => m_blobWriter.Write(aData, aOffset, aLength);
        public void WriteBool(bool aValue) => m_blobWriter.WriteBool(aValue);
        public void WriteInt8(sbyte aValue) => m_blobWriter.WriteInt8(aValue);
        public void WriteUInt8(byte aValue) => m_blobWriter.WriteUInt8(aValue);
        public void WriteInt16(short aValue) => m_blobWriter.WriteInt16(aValue);
        public void WriteUInt16(ushort aValue) => m_blobWriter.WriteUInt16(aValue);
        public void WriteInt32(int aValue) => m_blobWriter.WriteInt32(aValue);
        public void WriteUInt32(uint aValue) => m_blobWriter.WriteUInt32(aValue);
        public void WriteInt64(long aValue) => m_blobWriter.WriteInt64(aValue);
        public void WriteUInt64(ulong aValue) => m_blobWriter.WriteUInt64(aValue);
        public void WriteFloat(float aValue) => m_blobWriter.WriteFloat(aValue);
        public void WriteDouble(double aValue) => m_blobWriter.WriteDouble(aValue);
        public void WriteGuid(Guid value) => m_blobWriter.WriteGuid(value);
        public void WriteVector2(Vector2 value) => m_blobWriter.WriteVector2(value);
        public void WriteVector3(Vector3 value) => m_blobWriter.WriteVector3(value);
        public void WriteVector4(Vector4 value) => m_blobWriter.WriteVector4(value);
        public void WriteQuaternion(Quaternion value) => m_blobWriter.WriteQuaternion(value);
        public void WriteMatrix4x4(Matrix4x4 value) => m_blobWriter.WriteMatrix4x4(value);
        public void WriteReloc(Relocation aValue) => m_blobWriter.WriteReloc(aValue);

        public void RegisterObject(long offset, object ptr) => m_relocTable.RegisterObject(offset, ptr);
        public void AlignWriter(int alignment) => m_relocTable.AlignWriter(m_blobWriter, alignment);
        
        public void WriteEmptyArray()
        {
            WriteInt32(0);
            WriteInt32(0);
            WriteInt64(0);
        }

        public void WriteString(string value)
        {
            if (value == null)
            {
                WriteEmptyArray();
                return;
            }

            byte[] chars = Encoding.ASCII.GetBytes(value);
            WriteInt32(chars.Length + 1);
            WriteInt32(chars.Length + 1);
            WriteReloc(m_relocTable.RelocObject(Position, chars));
            long pos = Position;
            Position = Length;
            Length = Length + chars.Length + 1;
            m_relocTable.RegisterObject(Position, chars);
            m_blobWriter.Write(chars);
            m_blobWriter.WriteUInt8(0);
            Position = pos;
        }

        public bool BeginArray(int count, object obj)
        {
            if (count <= 0)
            {
                WriteEmptyArray();
                return false;
            }
            
            WriteInt32(count);
            WriteInt32(count);
            WriteReloc(m_relocTable.RelocObject(Position, obj));
            
            return true;
        }
        
        public void ReserveSpace(int count, int size, int align)
        {
            int num = DataUtil.ArraySize(size, align, count);
            Position = Length;
            m_relocTable.AlignWriter(m_blobWriter, align);
            Length += num;
        }

        public void WriteDataRef(ReflLayoutData value)
        {
            long pos = Position;
            WriteReloc(m_relocTable.RelocObject(pos, value));
            /*if (value != null && !m_relocTable.IsObjectRegistered(value))
            {
                long pos2 = Position;
                value.Save(m_blobWriter, m_relocTable);
                Position = pos2;
            }*/
        }
    }
}