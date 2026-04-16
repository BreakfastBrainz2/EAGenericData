using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace EAGenericData.IO
{
    public class ExtendedBinaryWriter : BinaryWriter
    {
        public Endian Endianness { get; set; } = Endian.Little;
        
        public long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }
        
        private Stack<long> m_steps = new Stack<long>();
        
        public ExtendedBinaryWriter(Stream stream, bool leaveOpen = false) : 
            base(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true), leaveOpen)
        {
            
        }

        public void StepIn(long position)
        {
            m_steps.Push(Position);
            Position = position;
        }

        public void StepOut()
        {
            Position = m_steps.Pop();
        }

        public void WriteBool(bool value) => Write(value);
        public void WriteInt8(sbyte value) => Write((sbyte)value);
        public void WriteUInt8(byte value) => Write((byte)value);

        public void WriteInt16(short value)
        {
            if (Endianness != Endian.Little)
            {
                Write((short)DataUtil.SwapEndian(value));
                return;
            }
            
            Write((short)value);
        }

        public void WriteUInt16(ushort value)
        {
            if (Endianness != Endian.Little)
            {
                Write((ushort)DataUtil.SwapEndian(value));
                return;
            }
            
            Write((ushort)value);
        }

        public void WriteInt32(int value)
        {
            if (Endianness != Endian.Little)
            {
                Write((int)DataUtil.SwapEndian(value));
                return;
            }
            
            Write((int)value);
        }

        public void WriteUInt32(uint value)
        {
            if(Endianness != Endian.Little)
            {
                Write((uint)DataUtil.SwapEndian(value));
                return;
            }
            
            Write((uint)value);
        }

        public void WriteInt64(long value)
        {
            if(Endianness != Endian.Little)
            {
                Write((long)DataUtil.SwapEndian(value));
                return;
            }
            
            Write((long)value);
        }

        public void WriteUInt64(ulong value)
        {
            if(Endianness != Endian.Little)
            {
                Write((ulong)DataUtil.SwapEndian(value));
                return;
            }
            
            Write((ulong)value);
        }
        
        public void WriteFloat(float value)
        {
            if (Endianness != Endian.Little)
            {
                FloatUnion union = new FloatUnion { Float = value };
                WriteUInt32(union.Value);
                return;
            }

            Write(value);
        }

        public void WriteDouble(double value)
        {
            if (Endianness != Endian.Little)
            {
                DoubleUnion union = new DoubleUnion { Double = value };
                WriteUInt64(union.Value);
                return;
            }

            Write(value);
        }
         
        public void WriteGuid(Guid value)
        {
            if (Endianness != Endian.Little)
            {
                byte[] b = value.ToByteArray();
                Write(b[3]); Write(b[2]); Write(b[1]); Write(b[0]);
                Write(b[5]); Write(b[4]);
                Write(b[7]); Write(b[6]);
                for (int i = 0; i < 8; i++)
                    Write(b[8 + i]);
                return;
            }
            
            Write(value.ToByteArray(), 0, 16);
        }

        public void WriteVector2(Vector2 value)
        {
            WriteFloat(value.X);
            WriteFloat(value.Y);
        }
        
        public void WriteVector3(Vector3 value)
        {
            WriteFloat(value.X);
            WriteFloat(value.Y);
            WriteFloat(value.Z);
        }
        
        public void WriteVector4(Vector4 value)
        {
            WriteFloat(value.X);
            WriteFloat(value.Y);
            WriteFloat(value.Z);
            WriteFloat(value.W);
        }
        
        public void WriteQuaternion(Quaternion value)
        {
            WriteFloat(value.X);
            WriteFloat(value.Y);
            WriteFloat(value.Z);
            WriteFloat(value.W);
        }

        public void WriteMatrix4x4(Matrix4x4 value)
        {
            WriteFloat(value.M11);
            WriteFloat(value.M12);
            WriteFloat(value.M13);
            WriteFloat(value.M14);
            WriteFloat(value.M21);
            WriteFloat(value.M22);
            WriteFloat(value.M23);
            WriteFloat(value.M24);
            WriteFloat(value.M31);
            WriteFloat(value.M32);
            WriteFloat(value.M33);
            WriteFloat(value.M34);
            WriteFloat(value.M41);
            WriteFloat(value.M42);
            WriteFloat(value.M43);
            WriteFloat(value.M44);
        }
    }
}