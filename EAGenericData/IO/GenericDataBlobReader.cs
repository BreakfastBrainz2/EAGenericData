using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
using EAGenericData.Layout;

namespace EAGenericData.IO
{
    public struct ArrayInfo
    {
        public int Count;
        public int Capacity;
        public long BeginOffset;
    }

    public struct DataBlockScopeInfo
    {
        public long StartOffset;
        public long DataOffset;
    }
    
    public class GenericDataBlobReader : IDataReader
    {
        public SortedList<ReflLayoutHash, ReflLayout> Layouts { get; private set; }
        public long Origin { get; set; } = -1;
        public long DataBlobStartOffset { get; set; } = -1;

        private Dictionary<long, object> mSharedData = new Dictionary<long, object>();
        private readonly ExtendedBinaryReader m_reader;

        public long Position
        {
            get => m_reader.Position;
            set => m_reader.Position = value;
        }

        public Endian Endianness
        {
            get => m_reader.Endianness;
            set => m_reader.Endianness = value;
        }
        
        public GenericDataBlobReader(ExtendedBinaryReader reader, SortedList<ReflLayoutHash, ReflLayout> layouts)
        {
            m_reader = reader;
            Layouts = layouts;
        }

        public byte[] ReadBytes(int count) => m_reader.ReadBytes(count);
        public bool ReadBool() => m_reader.ReadBoolean();
        public sbyte ReadInt8() => m_reader.ReadSByte();
        public byte ReadUInt8() => m_reader.ReadByte();
        public short ReadInt16() => m_reader.ReadInt16();
        public ushort ReadUInt16() => m_reader.ReadUInt16();
        public int ReadInt32() => m_reader.ReadInt32();
        public uint ReadUInt32() => m_reader.ReadUInt32();
        public long ReadInt64() => m_reader.ReadInt64();
        public ulong ReadUInt64() => m_reader.ReadUInt64();
        public float ReadFloat() => m_reader.ReadSingle();
        public double ReadDouble() => m_reader.ReadDouble();
        public Guid ReadGuid() => m_reader.ReadGuid();

        public void Seek(long offset, SeekOrigin origin) => m_reader.BaseStream.Seek(offset, origin);

        public void Register(long offset, object obj)
        {
            mSharedData.Add(offset, obj);
        }

        public object Fetch(long offset)
        {
            if (mSharedData.TryGetValue(offset, out var obj))
                return obj;

            return null;
        }

        public bool BeginArrayRead(out ArrayInfo info)
        {
            info.Capacity = ReadInt32();
            info.Count = ReadInt32();

            long relocPos = Position;
            long dataOffset = ReadInt64();
            info.BeginOffset = Position;
            if (info.Count > 0 && dataOffset != 0)
            {
                Position = DataBlobStartOffset + dataOffset;
                return true;
            }

            return false;
        }

        public string ReadGDString()
        {
            if (BeginArrayRead(out ArrayInfo info) && info.Count > 0)
            {
                byte[] chars = ReadBytes(info.Count);
                string str = Encoding.ASCII.GetString(chars, 0, chars.Length - 1);
                Position = info.BeginOffset;
                return str;
            }
            return null;
        }

        public ReflLayoutData ReadGDDataRef()
        {
            long offset = ReadInt64();
            if (offset != 0)
            {
                long actualOffset = offset + DataBlobStartOffset;
                object obj = Fetch(actualOffset);
                if (obj == null)
                {
                    long pos = Position;
                    Position = actualOffset;
                    ReflLayoutData data = ReflLayoutData.Load(this);
                    Position = pos;
                    return data;
                }

                return (ReflLayoutData)obj;
            }

            return null;
        }
        
        public Vector2 ReadVector2()
        {
            Vector2 value = new Vector2(ReadFloat(), ReadFloat());
            Position += 8; // padding
            return value;
        }

        public Vector3 ReadVector3()
        {
            Vector3 value = new Vector3(ReadFloat(), ReadFloat(), ReadFloat());
            Position += 4; // pad
            return value;
        }

        public Vector4 ReadVector4()
        {
            return new Vector4(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());
        }

        public Quaternion ReadQuaternion()
        {
            return new Quaternion(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());
        }

        public Matrix4x4 ReadMatrix4x4()
        {
            return new Matrix4x4(
                ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat(),
                ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat(),
                ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat(),
                ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat()
            );
        }
    }
}