using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
using EAGenericData.Serialization;

namespace EAGenericData.IO
{
    public class GenericDataBlobWriter : IDataWriter
    {
        public Endian Endianness
        {
            get => m_writer.Endianness;
            set => m_writer.Endianness = value;
        }
        
        public long Position
        {
            get => m_stream.Position - m_origin;
            set => m_stream.Position = m_origin + value;
        }

        public long Length
        {
            get => m_stream.Length - m_origin;
            set => m_stream.SetLength(value + m_origin);
        }
        
        public void Write(byte[] aData) => m_writer.Write(aData);
        public void Write(byte[] aData, int aOffset, int aLength) => m_writer.Write(aData, aOffset, aLength);
        public void WriteBool(bool aValue) => m_writer.WriteBool(aValue);
        public void WriteInt8(sbyte aValue) => m_writer.WriteInt8(aValue);
        public void WriteUInt8(byte aValue) => m_writer.WriteUInt8(aValue);
        public void WriteInt16(short aValue) => m_writer.WriteInt16(aValue);
        public void WriteUInt16(ushort aValue) => m_writer.WriteUInt16(aValue);
        public void WriteInt32(int aValue) => m_writer.WriteInt32(aValue);
        public void WriteUInt32(uint aValue) => m_writer.WriteUInt32(aValue);
        public void WriteInt64(long aValue) => m_writer.WriteInt64(aValue);
        public void WriteUInt64(ulong aValue) => m_writer.WriteUInt64(aValue);
        public void WriteFloat(float aValue) => m_writer.WriteFloat(aValue);
        public void WriteDouble(double aValue) => m_writer.WriteDouble(aValue);
        public void WriteGuid(Guid value) => m_writer.WriteGuid(value);

        public void WriteVector2(Vector2 value)
        {
            m_writer.WriteVector2(value);
            WriteFloat(0.0f);
            WriteFloat(0.0f);
        }

        public void WriteVector3(Vector3 value)
        {
            m_writer.WriteVector3(value);
            WriteFloat(0.0f);
        }
        public void WriteVector4(Vector4 value) => m_writer.WriteVector4(value);
        public void WriteQuaternion(Quaternion value) => m_writer.WriteQuaternion(value);
        public void WriteReloc(Relocation value) => WriteInt64(value.Offset);
        
        private readonly Stream m_stream;
        private readonly ExtendedBinaryWriter m_writer;
        private GenericDataFormat m_blobFormat;
        private long m_origin;

        private static readonly byte[] s_HeaderId_GD_DATA = Encoding.ASCII.GetBytes("GD.DATA");
        private static readonly byte[] s_HeaderId_GD_REFL = Encoding.ASCII.GetBytes("GD.REFL");
        private static readonly byte[] s_HeaderId_GD_STRM = Encoding.ASCII.GetBytes("GD.STRM");

        public GenericDataBlobWriter(Stream stream, Endian endian)
        {
            m_stream = stream;
            m_writer = new ExtendedBinaryWriter(stream);
            m_writer.Endianness = endian;
        }

        public void BeginBlob(GenericDataFormat format)
        {
            Debug.Assert(format != GenericDataFormat.INVALID_FORMAT);
            
            m_blobFormat = format;
            m_origin = m_stream.Position;
            Length = 0;

            WriteBlobHeader();
        }

        public void WriteBlobHeader()
        {
            switch (m_blobFormat)
            {
                case GenericDataFormat.GD_DATA: m_writer.Write(s_HeaderId_GD_DATA); break;
                case GenericDataFormat.GD_REFL: m_writer.Write(s_HeaderId_GD_REFL); break;
                case GenericDataFormat.GD_STRM: m_writer.Write(s_HeaderId_GD_STRM); break;
                default: throw new NotImplementedException();
            }
            m_writer.Write(Endianness == Endian.Little ? 'l' : 'b');

            m_writer.WriteUInt32(0); // temp - blob size
        }

        public int EndBlob()
        {
            Position = 8;
            m_writer.WriteUInt32((uint)Length);
            Position = Length;

            return (int)Length;
        }
    }
}