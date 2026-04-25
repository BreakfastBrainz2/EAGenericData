using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using EAGenericData.IO;
using EAGenericData.Serialization;

namespace EAGenericData.Layout
{
    public class ReflLayoutData
    {
        private const int DATA_OBJECT_HEADER_SIZE = 32;

        private const ulong GD_DATA_VTABLE_ID = 0x30E6205;

        public Dictionary<string, object> ValueByName = new Dictionary<string, object>();
        public ReflLayout Layout;
        public long StartOffset;
        public long DataOffset;
        public int DataSize;

        internal ReflLayoutData() {}

        internal ReflLayoutData(ReflLayout layout)
        {
            Layout = layout;
        }
        
        internal ReflLayoutData(ReflLayout layout, long startOffset, ushort dataOffset)
        {
            Layout = layout;
            StartOffset = startOffset;
            DataOffset = startOffset + dataOffset;
        }

        public static ReflLayoutData CreateNew(ReflLayout layout)
        {
            ReflLayoutData data = new ReflLayoutData(layout);
            data.CreateDefaultValues();

            return data;
        }

        public void SetValue<T>(ReflLayout.FieldEntry field, T value)
        {
            ValueByName[field.FixedName] = value;
        }

        public void SetValue<T>(string fieldName, T value)
        {
            if (Layout.ValidEntries.All(x => x.Name != fieldName))
                throw new InvalidDataException($"Field {fieldName} does not exist");
            
            ValueByName[fieldName] = value;
        }

        public void SetValue<T>(int entryId, T value)
        {
            SetValue(Layout.Entries[entryId - Layout.MinSlot], value);
        }

        public T GetValue<T>(int entryId)
        {
            return (T)ValueByName[Layout.Entries[entryId - Layout.MinSlot].FixedName];
        }

        public T GetValue<T>(string fieldName)
        {
            return (T)ValueByName[fieldName];
        }

        internal void CreateDefaultValues()
        {
            foreach(var fld in Layout.ValidEntries)
            {
                ValueByName.Add(fld.FixedName, CreateDefaultValueForField(fld));
            }
        }

        // this needs work, iirc results are not what they should be
        public override int GetHashCode()
        {
            uint hash = (uint)Layout.LayoutHash;
            foreach (var entry in Layout.ValidEntries)
            {
                switch (entry.FieldCategory)
                {
                    case ReflFieldCategory.Value:
                    {
                        hash = HashValue(entry.Layout.LayoutHash, ValueByName[entry.FixedName], hash);
                        break;
                    }
                    case ReflFieldCategory.Nested:
                    {
                        ReflLayoutData nested = (ReflLayoutData)ValueByName[entry.FixedName];
                        hash = (uint)nested.GetHashCode();
                        break;
                    }
                    case ReflFieldCategory.ArrayValue:
                    {
                        hash = Crc32.HashArray((dynamic)ValueByName[entry.FixedName], hash);
                        break;
                    }
                    case ReflFieldCategory.ListValue:
                    {
                        hash = Crc32.HashList((dynamic)ValueByName[entry.FixedName], hash);
                        break;
                    }
                    case ReflFieldCategory.ArrayNested:
                    {
                        hash = Crc32.HashNestedArray((dynamic)ValueByName[entry.FixedName], hash);
                        break;
                    }
                    case ReflFieldCategory.ListNested:
                    {
                        hash = Crc32.HashNestedList((dynamic)ValueByName[entry.FixedName], hash);
                        break;
                    }
                    case ReflFieldCategory.ArrayListValue:
                    {
                        hash = Crc32.HashArrayList((dynamic)ValueByName[entry.FixedName], hash);
                        break;
                    }
                    case ReflFieldCategory.ArrayListNested:
                    {
                        hash = Crc32.HashArrayListNested((dynamic)ValueByName[entry.FixedName], hash);
                        break;
                    }
                }
            }

            return (int)hash;
        }

        public override string ToString()
        {
            if (Layout != null)
            {
                return $"{Layout.Name} (0x{StartOffset:X})";
            }

            return $"{StartOffset:X}";
        }
        
        internal uint HashValue(ReflLayoutHash hash, object value, uint result)
        {
            switch (hash)
            {
                case ReflLayoutHash.Bool: return Crc32.Hash((bool)value, result);
                case ReflLayoutHash.Int8: return Crc32.Hash((sbyte)value, result);
                case ReflLayoutHash.UInt8: return Crc32.Hash((byte)value, result);
                case ReflLayoutHash.Int16: return Crc32.Hash((short)value, result);
                case ReflLayoutHash.UInt16: return Crc32.Hash((ushort)value, result);
                case ReflLayoutHash.Int32: return Crc32.Hash((int)value, result); 
                case ReflLayoutHash.UInt32: return Crc32.Hash((uint)value, result);
                case ReflLayoutHash.Int64: return Crc32.Hash((long)value, result);
                case ReflLayoutHash.UInt64: return Crc32.Hash((ulong)value, result);
                case ReflLayoutHash.Float: return Crc32.Hash((float)value, result);
                case ReflLayoutHash.Double: return Crc32.Hash((double)value, result);
                case ReflLayoutHash.Vector2: return Crc32.Hash((Vector2)value, result);
                case ReflLayoutHash.Vector3: return Crc32.Hash((Vector3)value, result);
                case ReflLayoutHash.Vector4: return Crc32.Hash((Vector4)value, result);
                case ReflLayoutHash.Quaternion: return Crc32.Hash((Quaternion)value, result);
                case ReflLayoutHash.Matrix44: return Crc32.Hash((Matrix4x4)value, result);
                case ReflLayoutHash.Guid: return Crc32.Hash((Guid)value, result);
                case ReflLayoutHash.String: return Crc32.Hash((string)value, result);
                case ReflLayoutHash.DataRef: return Crc32.Hash((ReflLayoutData)value, result);
                default: throw new NotImplementedException($"Need to implement {hash}");
            }
        }

        private object CreateDefaultValueForField(ReflLayout.FieldEntry entry)
        {
            switch (entry.FieldCategory)
            {
                case ReflFieldCategory.Value:
                {
                    switch (entry.Layout.LayoutHash)
                    {
                        case ReflLayoutHash.String: return string.Empty;
                        case ReflLayoutHash.DataRef: return null;
                    }

                    Type fieldType = ReflLayoutType.GetConcreteType(entry.LayoutHash);
                    return Activator.CreateInstance(fieldType, true);
                }
                case ReflFieldCategory.ListValue:
                {
                    Type listType = typeof(List<>).MakeGenericType(ReflLayoutType.GetConcreteType(entry.LayoutHash));
                    return Activator.CreateInstance(listType);
                }
                default: throw new NotImplementedException();
            }
        }

        #region Loading methods

        public static ReflLayoutData Load(GenericDataBlobReader blobReader)
        {
            long begin = blobReader.Position;
            ulong vtableId = blobReader.ReadUInt64();
            if (vtableId != GD_DATA_VTABLE_ID)
            {
                throw new InvalidDataException("Invalid GD.DATA blob");
            }

            blobReader.Position += 8; // allocator
            ReflLayoutHash layoutHash = (ReflLayoutHash)blobReader.ReadUInt64();
            blobReader.Position += 4; // refCount
            ushort dataOffset = blobReader.ReadUInt16();
            blobReader.Position += 2; // mutable

            ReflLayout layout = blobReader.Layouts[layoutHash];
            ReflLayoutData data = new ReflLayoutData();
            blobReader.Register(begin, data);
            
            data.Layout = layout;
            data.StartOffset = begin;
            data.DataOffset = begin + dataOffset;
            if (layout.DataSize > 0)
            {
                blobReader.Position = begin + dataOffset;
                data.LoadData(blobReader);
            }
            
            return data;
        }
        
        private void LoadData(GenericDataBlobReader blobReader)
        {
            for (int entryIt = 0; entryIt < Layout.ValidEntries.Length; entryIt++)
            {
                var entry = Layout.ValidEntries[entryIt];
                blobReader.Position = DataOffset + entry.Offset;
                switch (entry.FieldCategory)
                {
                    // value type
                    case ReflFieldCategory.Value:
                    {
                        object value = ReadValue(blobReader, entry.Layout.LayoutHash);
                        ValueByName.Add(entry.FixedName, value);
                        break;
                    }
                    // array of value types
                    case ReflFieldCategory.ArrayValue:
                    {
                        Type elementType = ReflLayoutType.GetConcreteType(entry.Layout.LayoutHash);
                        Array array = Array.CreateInstance(elementType, entry.Count);
                        for (int i = 0; i < array.Length; i++)
                        {
                            array.SetValue(ReadValue(blobReader, entry.Layout.LayoutHash), i);
                        }

                        ValueByName.Add(entry.FixedName, array);
                        break;
                    }
                    // list of value types
                    case ReflFieldCategory.ListValue:
                    {
                        Type elementType = ReflLayoutType.GetConcreteType(entry.Layout.LayoutHash);
                        bool hasArray = blobReader.BeginArrayRead(out ArrayInfo info);
                        
                        var values = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType), args: info.Count);
                        if (hasArray)
                        {
                            for (int i = 0; i < info.Count; i++)
                            {
                                values.Add(ReadValue(blobReader, entry.Layout.LayoutHash));
                            }

                            blobReader.Position = info.BeginOffset;
                        }

                        ValueByName.Add(entry.FixedName, values);
                        break;
                    }
                    // nested structure
                    case ReflFieldCategory.Nested:
                    {
                        Type elementType = ReflLayoutType.GetConcreteType(entry.Layout.LayoutHash);
                        Debug.Assert(elementType == typeof(ReflLayoutData));
                        ReflLayoutData nested = new ReflLayoutData(entry.Layout, blobReader.Position, 0);
                        nested.LoadData(blobReader);
                        ValueByName.Add(entry.FixedName, nested);
                        break;
                    }
                    // array of nested structures
                    case ReflFieldCategory.ArrayNested:
                    {
                        Type elementType = ReflLayoutType.GetConcreteType(entry.Layout.LayoutHash);
                        Debug.Assert(elementType == typeof(ReflLayoutData));
                        Array array = Array.CreateInstance(elementType, entry.Count);
                        
                        int align = DataUtil.Align(entry.Layout.DataSize, entry.Layout.Alignment);
                        long pos = blobReader.Position;
                        for (int i = 0; i < entry.Count; i++)
                        {
                            blobReader.Position = pos + align * i;
                            ReflLayoutData nested = new ReflLayoutData(entry.Layout, blobReader.Position, 0);
                            nested.LoadData(blobReader);
                            array.SetValue(nested, i);
                        }

                        ValueByName.Add(entry.FixedName, array);
                        break;
                    }
                    // list of nested structures
                    case ReflFieldCategory.ListNested:
                    {
                        Type elementType = ReflLayoutType.GetConcreteType(entry.Layout.LayoutHash);
                        Debug.Assert(elementType == typeof(ReflLayoutData));
                        var values = new List<ReflLayoutData>();
                        if (blobReader.BeginArrayRead(out ArrayInfo info))
                        {
                            int align = DataUtil.Align(entry.Layout.DataSize, entry.Layout.Alignment);
                            long pos = blobReader.Position;
                            for (int i = 0; i < info.Count; i++)
                            {
                                blobReader.Position = pos + align * i;
                                ReflLayoutData nested = new ReflLayoutData(entry.Layout, blobReader.Position, 0);
                                nested.LoadData(blobReader);
                                values.Add(nested);
                            }
                            
                            blobReader.Position = info.BeginOffset;
                        }

                        ValueByName.Add(entry.FixedName, values);
                        break;
                    }
                    default: throw new InvalidDataException($"Unimplemented category: {entry.FieldCategory}");
                }
            }
        }
        
        private object ReadValue(GenericDataBlobReader blobReader, ReflLayoutHash hash)
        {
            switch (hash)
            {
                case ReflLayoutHash.Bool: return blobReader.ReadBool();
                case ReflLayoutHash.Int8: return blobReader.ReadInt8();
                case ReflLayoutHash.UInt8: return blobReader.ReadUInt8();
                case ReflLayoutHash.Int16: return blobReader.ReadInt16();
                case ReflLayoutHash.UInt16: return blobReader.ReadUInt16();
                case ReflLayoutHash.Int32: return blobReader.ReadInt32();
                case ReflLayoutHash.UInt32: return blobReader.ReadUInt32();
                case ReflLayoutHash.Int64: return blobReader.ReadInt64();
                case ReflLayoutHash.UInt64: return blobReader.ReadUInt64();
                case ReflLayoutHash.Float: return blobReader.ReadFloat();
                case ReflLayoutHash.Double: return blobReader.ReadDouble();
                case ReflLayoutHash.Vector2: return blobReader.ReadVector2();
                case ReflLayoutHash.Vector3: return blobReader.ReadVector3();
                case ReflLayoutHash.Vector4: return blobReader.ReadVector4();
                case ReflLayoutHash.Quaternion: return blobReader.ReadQuaternion();
                case ReflLayoutHash.Matrix44: return blobReader.ReadMatrix4x4();
                case ReflLayoutHash.Guid: return blobReader.ReadGuid();
                case ReflLayoutHash.String: return blobReader.ReadGDString();
                case ReflLayoutHash.DataRef: return blobReader.ReadGDDataRef();
                default: throw new NotImplementedException($"Need to implement {hash}");
            }
        }
        
        #endregion
        
        #region Saving methods
        
        public void Save(GenericDataBlobWriter writer, RelocationTable relocTable)
        {
            writer.Position = writer.Length;
            
            long beginPos = relocTable.AlignWriter(writer, 8);
            relocTable.RegisterObject(beginPos, this);

            long dataBeginPos = beginPos + DATA_OBJECT_HEADER_SIZE;
            dataBeginPos = relocTable.AlignPosition(dataBeginPos, Layout.Alignment) - beginPos;
            
            writer.WriteUInt64(GD_DATA_VTABLE_ID);
            writer.WriteUInt64(0); // allocator - always 0
            writer.WriteUInt64((ulong)Layout.LayoutHash);
            writer.WriteUInt32(0); // ref count - always 0
            writer.WriteUInt16((ushort)dataBeginPos); // data offset
            writer.WriteUInt16(0); // mutable - always 0
            Debug.Assert(writer.Position - beginPos == DATA_OBJECT_HEADER_SIZE);

            if (Layout.DataSize > 0)
            {
                relocTable.AlignWriter(writer, Layout.Alignment);
                Debug.Assert(writer.Position - beginPos == dataBeginPos);
                // preallocate enough data to hold this asset
                writer.Length = writer.Position + Layout.DataSize;

                GenericDataWriter gdWriter = new GenericDataWriter(writer, relocTable);
                SaveData(gdWriter);
            }
        }
        
        private void SaveData(GenericDataWriter writer)
        {
            long dataBeginPos = writer.Position;
            foreach (var entry in Layout.ValidEntries)
            {
                object value = ValueByName[entry.FixedName];
                writer.Position = dataBeginPos + entry.Offset;
                //Console.WriteLine($"Writing {entry.Layout.Name} {entry.Name} at 0x{writer.Position:X}");
                switch (entry.FieldCategory)
                {
                    case ReflFieldCategory.Value:
                        WriteValue(writer, entry.Layout.LayoutHash, value);
                        break;
                    case ReflFieldCategory.ArrayValue:
                    {
                        Array array = (Array)value;
                        Debug.Assert(entry.Count > 1);
                        {
                            for (int i = 0; i < entry.Count; i++)
                            {
                                WriteValue(writer, entry.Layout.LayoutHash, array.GetValue(i));
                            }
                        }
                        break;
                    }
                    case ReflFieldCategory.ListValue:
                    {
                        IList list = (IList)value;
                        if (writer.BeginArray(list.Count, value))
                        {
                            writer.ReserveSpace(list.Count, entry.Layout.DataSize, entry.Layout.Alignment);
                            writer.RegisterObject(writer.Position, value);
                            
                            long arrayBeginPos = writer.Position;
                            long stride = DataUtil.Align(entry.Layout.DataSize, entry.Layout.Alignment);
                            for(int i = 0; i < list.Count; i++)
                            {
                                writer.Position = DataUtil.Align(arrayBeginPos + i * stride, entry.Layout.Alignment);
                                WriteValue(writer, entry.Layout.LayoutHash, list[i]);
                            }
                        }
                        break;
                    }
                    case ReflFieldCategory.Nested:
                        ((ReflLayoutData)value).SaveData(writer);
                        break;
                    case ReflFieldCategory.ListNested:
                    {
                        IList list = (IList)value;
                        if (writer.BeginArray(list.Count, value))
                        {
                            long pos = writer.Position;
                            writer.ReserveSpace(list.Count, entry.Layout.DataSize, entry.Layout.Alignment);
                            writer.RegisterObject(writer.Position, value);

                            long arrayBeginPos = writer.Position;
                            long stride = DataUtil.Align(entry.Layout.DataSize, entry.Layout.Alignment);
                            for (int i = 0; i < list.Count; i++)
                            {
                                writer.Position = DataUtil.Align(arrayBeginPos + i * stride, entry.Layout.Alignment);
                                ReflLayoutData nestedData = (ReflLayoutData)list[i];
                                nestedData.SaveData(writer);
                            }

                            writer.Position = pos;
                        }
                        break;
                    }
                    default:
                        throw new InvalidDataException($"Unimplemented write category {entry.FieldCategory} for field {entry.Name}");
                        break;
                }
            }
        }
        
        public void WriteValue(GenericDataWriter writer, ReflLayoutHash hash, object value)
        {
            //Console.WriteLine($"ReflLayoutData: writing a {hash} at 0x{writer.Position:X}");
            switch (hash)
            {
                case ReflLayoutHash.Bool: writer.WriteBool((bool)value); break;
                case ReflLayoutHash.Int8: writer.WriteInt8((sbyte)value); break;
                case ReflLayoutHash.UInt8: writer.WriteUInt8((byte)value); break;
                case ReflLayoutHash.Int16: writer.WriteInt16((short)value); break;
                case ReflLayoutHash.UInt16: writer.WriteUInt16((ushort)value); break;
                case ReflLayoutHash.Int32: writer.WriteInt32((int)value); break;
                case ReflLayoutHash.UInt32: writer.WriteUInt32((uint)value); break;
                case ReflLayoutHash.Int64: writer.WriteInt64((long)value); break;
                case ReflLayoutHash.UInt64: writer.WriteUInt64((ulong)value); break;
                case ReflLayoutHash.Float: writer.WriteFloat((float)value); break;
                case ReflLayoutHash.Double: writer.WriteDouble((double)value); break;
                case ReflLayoutHash.Guid: writer.WriteGuid((Guid)value); break;
                case ReflLayoutHash.Vector2: writer.WriteVector2((Vector2)value); break;
                case ReflLayoutHash.Vector3: writer.WriteVector3((Vector3)value); break;
                case ReflLayoutHash.Vector4: writer.WriteVector4((Vector4)value); break;
                case ReflLayoutHash.Quaternion: writer.WriteQuaternion((Quaternion)value); break;
                case ReflLayoutHash.Matrix44: writer.WriteMatrix4x4((Matrix4x4)value); break;
                case ReflLayoutHash.String: writer.WriteString((string)value); break;
                case ReflLayoutHash.DataRef: writer.WriteDataRef((ReflLayoutData)value); break;
                default: throw new NotImplementedException($"Need to implement {hash}");
            }
        }
        
        #endregion
    }
}