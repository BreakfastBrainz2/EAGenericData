using System.IO;

namespace EAGenericData
{
    public class DebuggableMemoryStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            base.WriteByte(value);
        }
    }
}