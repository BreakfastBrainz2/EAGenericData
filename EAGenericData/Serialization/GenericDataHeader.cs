using System.IO;
using System.Text;
using EAGenericData.IO;

namespace EAGenericData.Serialization
{
    public struct GenericDataHeader
    {
        public const int SIZE_IN_BYTES = 12;
        
        public GenericDataFormat Format;
        public Endian Endian;
        public int Size;
        
        public static GenericDataFormat GetHeaderFormat(string idStr)
        {
            switch (idStr)
            {
                case "GD.DATA": return GenericDataFormat.GD_DATA;
                case "GD.DAT2": return GenericDataFormat.GD_DAT2;
                case "GD.STRM": return GenericDataFormat.GD_STRM;
                case "GD.REFL": return GenericDataFormat.GD_REFL;
                case "GD.REF2": return GenericDataFormat.GD_REF2;
                default: return GenericDataFormat.INVALID_FORMAT;
            }
        }
        
        public static GenericDataHeader LoadHeader(ExtendedBinaryReader reader)
        {
            byte[] idBytes = new byte[8];
            reader.Read(idBytes, 0, 8);
            string idStr = Encoding.ASCII.GetString(idBytes, 0, 7);

            GenericDataHeader header = new GenericDataHeader();
            header.Format = GenericDataHeader.GetHeaderFormat(idStr);

            if (header.Format == GenericDataFormat.INVALID_FORMAT)
            {
                throw new InvalidDataException($"Invalid GenericData format: {idStr}");
            }

            if (idBytes[7] != 'l' && idBytes[7] != 'b')
            {
                throw new InvalidDataException($"Invalid GenericData endianness: {Encoding.UTF8.GetString(idBytes, 6, 1)}");
            }

            header.Endian = idBytes[7] == 'l' ? Endian.Little : Endian.Big;
            header.Size = reader.ReadInt32(header.Endian);

            return header;
        }
    }
}