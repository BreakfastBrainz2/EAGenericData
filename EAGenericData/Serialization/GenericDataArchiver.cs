using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using EAGenericData.Frostbite;
using EAGenericData.IO;
using EAGenericData.Layout;

namespace EAGenericData.Serialization
{
    public static class GenericDataArchiver
    {
        #region Loading methods
        
        public static List<ReflLayoutData> LoadStream(Stream stream, bool isFrostbitePackage)
        {
            using (ExtendedBinaryReader reader = new ExtendedBinaryReader(stream))
            {
                if (isFrostbitePackage)
                {
                    AntPackagingType packageType = (AntPackagingType)reader.ReadUInt32(Endian.Big);
                    if (packageType != AntPackagingType.AnimationSet)
                    {
                        StaticAntPackageMeta meta = LoadPackageMeta(reader);
                    }
                }
                
                return LoadStream(reader);
            }
        }
    
        public static List<ReflLayoutData> LoadStream(ExtendedBinaryReader reader)
        {
            GenericDataHeader header = GenericDataHeader.LoadHeader(reader);
            if (header.Format != GenericDataFormat.GD_STRM)
                throw new InvalidDataException($"Unsupported first blob format: {header.Format}");

            reader.Endianness = header.Endian;

            long strmEndOffset = reader.Position + (header.Size - GenericDataHeader.SIZE_IN_BYTES);
            uint biggestBlobSize = reader.ReadUInt32();

            header = GenericDataHeader.LoadHeader(reader);
            if (header.Format != GenericDataFormat.GD_REFL)
                throw new InvalidDataException($"Expected first blob to be 'REFL', but got {header.Format}");
                
            var layouts = LoadREFLBlob(reader, header);
            
            List<ReflLayoutData> data = new List<ReflLayoutData>();
            GenericDataBlobReader blobReader = new GenericDataBlobReader(reader, layouts);
            while (reader.Position < strmEndOffset)
            {
                header = GenericDataHeader.LoadHeader(reader);
                if (header.Format != GenericDataFormat.GD_DATA)
                    throw new InvalidDataException($"Unsupported blob format: {header.Format}");

                var dataBlob = LoadDATABlob(blobReader, header);
                data.Add(dataBlob);
            }
            
            return data;
        }
        
        public static ReflLayoutCollection LoadREFLBlob(ExtendedBinaryReader reader, GenericDataHeader reflHeader)
        {
            long endOffset = reader.BaseStream.Position + (reflHeader.Size - GenericDataHeader.SIZE_IN_BYTES);

            reader.Endianness = reflHeader.Endian;

            int relocTableOffset = reader.ReadInt32();

            long layoutTableBegin = reader.BaseStream.Position;
            ulong layoutCount = reader.ReadUInt64();
            
            SortedList<long, ReflLayout> layoutPtrTable = new SortedList<long, ReflLayout>();
            for (ulong i = 0; i < layoutCount; i++)
            {
                layoutPtrTable.Add(reader.ReadInt64(), new ReflLayout());
            }

            ReflLayoutCollection layouts = new ReflLayoutCollection();
            foreach (var reloc in layoutPtrTable)
            {
                reader.BaseStream.Position = layoutTableBegin + reloc.Key;
                reloc.Value.Load(reader, layoutPtrTable);
                layouts.Add(reloc.Value.LayoutHash, reloc.Value);
            }

            foreach (var kvp in layouts)
            {
                kvp.Value.PostLoad();
                //Console.WriteLine(kvp.Value.DumpInfo());
            }

            reader.BaseStream.Position = endOffset;
            return layouts;
        }
        
        public static ReflLayoutData LoadDATABlob(GenericDataBlobReader reader, GenericDataHeader header)
        {
            reader.Origin = reader.Position - GenericDataHeader.SIZE_IN_BYTES;
            long endOffset = reader.Position + (header.Size - GenericDataHeader.SIZE_IN_BYTES);

            reader.Endianness = header.Endian;
            // Each data blob contains a reloc table, which is used to fixup relative pointers at runtime.
            // Format:
            // uint NumRelocs
            // uint Relocs[NumRelocs]
            // To fixup pointers, the runtime does this:
            // foreach(uint reloc in Relocs)
            // {
            //      uint* ptr = DataBlobPtr + reloc;
            //      *ptr += DataBlobPtr;
            // }
            // We don't need the table, since we can just add DataBlobStartOffset to relative offsets.
            uint relocTableOffset = reader.ReadUInt32();
            
            reader.DataBlobStartOffset = reader.Position;

            ReflLayoutData data = ReflLayoutData.Load(reader);
            data.DataSize = header.Size;
            reader.Seek(endOffset, SeekOrigin.Begin);
            return data;
        }
        
        public static StaticAntPackageMeta LoadPackageMeta(ExtendedBinaryReader reader)
        {
            reader.Endianness = Endian.Big; // frostbite header is always big endian
            StaticAntPackageMeta meta = new StaticAntPackageMeta();
            
            meta.MetaBytes = reader.ReadUInt32();
            meta.PackageType = (AntPackagingType)reader.ReadUInt32();
            meta.AssetCount = reader.ReadUInt32();
            meta.ImportCount = reader.ReadUInt32();
            meta.AssetBytes = reader.ReadUInt32();
            meta.ExtraBytes = reader.ReadUInt32();

            if (meta.ExtraBytes > 0)
            {
                meta.TotalNonStaticAssetCount = reader.ReadUInt32();
                meta.MaxNonStaticAssetCount = reader.ReadUInt32();
                meta.MaxNonStaticImportCount = reader.ReadUInt32();
                meta.StaticBundleImportBucketSize = reader.ReadUInt32();
                meta.StaticBundleImportBucketEntries = reader.ReadUInt32();
                meta.StaticBundleFlatImportCount = reader.ReadUInt32();
                meta.StaticBundleImportCountInSlots = reader.ReadUInt32();
                meta.StaticGuidToIntSize = reader.ReadUInt32();
                
                // TODO: read guid to int map
                // TODO: read static import map

                reader.BaseStream.Position = 0x14;
                reader.BaseStream.Position += meta.MetaBytes;
            }

            return meta;
        }
        
        #endregion

        #region Saving methods

        public static void SaveStream(Stream stream, Endian endian, List<ReflLayoutData> data, ReflLayoutCollection layouts)
        {
            GenericDataBlobWriter blobWriter = new GenericDataBlobWriter(stream, endian);
            blobWriter.BeginBlob(GenericDataFormat.GD_STRM);
            blobWriter.WriteUInt32(DataUtil.PTR_PLACEHOLDER);

            int biggestBlobSize = SaveREFL(stream, layouts);

            foreach (var asset in data)
            {
                int dataBlobSize = SaveData(stream, asset);
                biggestBlobSize = Math.Max(biggestBlobSize, dataBlobSize);
            }

            blobWriter.Position = 0xC;
            blobWriter.WriteInt32(biggestBlobSize);

            blobWriter.EndBlob();
        }
        
        public static int SaveREFL(Stream stream, ReflLayoutCollection layouts)
        {
            GenericDataBlobWriter blobWriter = new GenericDataBlobWriter(stream, Endian.Big);
            blobWriter.BeginBlob(GenericDataFormat.GD_REFL);
            blobWriter.WriteUInt32(DataUtil.PTR_PLACEHOLDER); // reloc table offset

            RelocationTable relocTable = new RelocationTable(blobWriter.Position);
            
            blobWriter.WriteUInt64((ulong)layouts.Count);
            foreach (var lyt in layouts)
            {
                long pos = blobWriter.Position;
                blobWriter.WriteReloc(relocTable.RelocPtr(pos, (ulong)lyt.Key));
            }

            foreach (var lyt in layouts)
            {
                lyt.Value.Save(blobWriter, relocTable);
            }

            relocTable.AlignWriter(blobWriter, 4);
            long relocTableOffset = blobWriter.Position;
            blobWriter.Position = 0xC;
            blobWriter.WriteUInt32((uint)relocTableOffset);
            blobWriter.Position = relocTableOffset;
            
            relocTable.WriteRelocTable(blobWriter);
            
            return blobWriter.EndBlob();
        }

        public static int SaveData(Stream stream, ReflLayoutData data)
        {
            GenericDataBlobWriter blobWriter = new GenericDataBlobWriter(stream, Endian.Big);
            blobWriter.BeginBlob(GenericDataFormat.GD_DATA);
            blobWriter.WriteUInt32(DataUtil.PTR_PLACEHOLDER); // reloc table offset
            
            RelocationTable relocTable = new RelocationTable(blobWriter.Position);
            
            data.Save(blobWriter, relocTable);
            blobWriter.Position = blobWriter.Length;
            
            relocTable.AlignWriter(blobWriter, 4);
            long relocTableOffset = blobWriter.Position;
            blobWriter.Position = 0xC;
            blobWriter.WriteUInt32((uint)relocTableOffset);
            blobWriter.Position = relocTableOffset;
            
            relocTable.WriteRelocTable(blobWriter);
            
            return blobWriter.EndBlob();
        }

        public static void CollectLayoutTypes(ReflLayout layout, ReflLayoutCollection reflLayouts)
        {
            reflLayouts.TryAddLayout(layout);
            foreach (var entry in layout.ValidEntries)
            {
                CollectLayoutTypes(entry.Layout, reflLayouts);
            }
        }

        public static void CollectLayoutTypes(ReflLayoutData data, ReflLayoutCollection reflLayouts)
        {
            if (data == null)
                return;
            
            CollectLayoutTypes(data.Layout, reflLayouts);
            foreach (var entry in data.Layout.ValidEntries)
            {
                if (entry.Layout == ReflLayoutType.DataRef && entry.FieldCategory == ReflFieldCategory.Value)
                {
                    CollectLayoutTypes((ReflLayoutData)data.ValueByName[entry.Name], reflLayouts);
                }
                else if (entry.Layout == ReflLayoutType.DataRef || !entry.Layout.IsNativeType)
                {
                    CollectLayoutTypes(entry.Layout, reflLayouts);
                }
            }
        }
        
        #endregion
        
        // Verify that serialization works correctly by rewriting all input data
        internal static void TestSerialization(List<ReflLayoutData> data, ReflLayoutCollection layouts)
        {
            DebuggableMemoryStream strm = new DebuggableMemoryStream();
            ReflLayoutCollection layoutTypes = new ReflLayoutCollection();
            ExtendedBinaryReader testBR = new ExtendedBinaryReader(strm);
            
            GenericDataHeader header;
            for (int i = 0; i < data.Count; i++)
            {
                ReflLayoutData asset = data[i];
                CollectLayoutTypes(asset, layoutTypes);
                long blobPos = strm.Position;
                //try
                {
                    SaveData(strm, asset);
                }// catch (Exception e) {}
                testBR.Position = blobPos;
                GenericDataBlobReader testBlobR = new GenericDataBlobReader(testBR, layouts);
                try
                {
                    header = GenericDataHeader.LoadHeader(testBR);
                    if (header.Size != asset.DataSize)
                    {
                        throw new SerializationException(
                            $"Asset size mismatch: expected {asset.DataSize}, got {header.Size}");
                    }
                    ReflLayoutData testReload = LoadDATABlob(testBlobR, header);
                }
                catch (Exception e)
                {
                    using (var fs = new FileStream("failure.genericdata", FileMode.Create, FileAccess.Write))
                    {
                        strm.WriteTo(fs);
                    }
                    Console.WriteLine($"SERIALIZATION FAILED on asset {i} {asset.Layout.Name} {asset.ValueByName["__name"]} - {asset.ValueByName["__guid"]}: {e}");
                    throw;
                }
            }
            using (var fs = new FileStream("success.genericdata", FileMode.Create, FileAccess.Write))
            {
                strm.WriteTo(fs);
            }
            strm.Close();
        }
    }   
}