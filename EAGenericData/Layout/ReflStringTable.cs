using System.Collections.Generic;
using System.Text;
using EAGenericData.IO;

namespace EAGenericData.Layout
{
    public class ReflStringTable
    {
        private List<string> m_table = new List<string>();
        private Dictionary<int, string> m_offsetToString = new Dictionary<int, string>();

        public ReflStringTable() {}

        public ReflStringTable(ExtendedBinaryReader reader, int numBytes)
        {
            long start = reader.Position;
            long end = reader.Position + numBytes;
            
            while (reader.Position < end)
            {
                int pos = (int)reader.Position - (int)start;
                string str = reader.ReadNullTerminatedString();
                m_offsetToString.Add(pos, str);
                m_table.Add(str);
            }
        }

        public int Add(string item)
        {
            int idx = m_table.Count;
            m_table.Add(item);

            return idx;
        }

        public string GetByIndex(int index)
        {
            return m_table[index];
        }

        public string GetByOffset(int offset)
        {
            if (m_offsetToString.TryGetValue(offset, out var value))
                return value;

            return null;
        }

        public int GetTableSize()
        {
            int size = 0;
            foreach (var str in m_table)
            {
                size += Encoding.ASCII.GetByteCount(str) + 1;
            }

            return size;
        }

        public byte[] ToByteArray()
        {
            int totalLength = 0;

            foreach (var str in m_table)
            {
                totalLength += str.Length + 1;
            }

            byte[] result = new byte[totalLength];
            int offset = 0;

            foreach (var str in m_table)
            {
                int written = Encoding.ASCII.GetBytes(str, 0, str.Length, result, offset);

                offset += written;
                result[offset++] = 0;
            }

            return result;
        }
    }
}