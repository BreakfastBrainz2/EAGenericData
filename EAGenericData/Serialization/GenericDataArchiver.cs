using EAGenericData.Frostbite;
using EAGenericData.IO;
using EAGenericData.Layout;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace EAGenericData.Serialization
{
    public static class GenericDataArchiver
    {
        #region Loading methods

        public static AntAssetBank LoadBank(Stream stream)
        {
            using (ExtendedBinaryReader reader = new ExtendedBinaryReader(stream))
            {
                AntAssetBank bank = new AntAssetBank();

                AntPackagingType packageType = (AntPackagingType)reader.ReadUInt32(Endian.Big);
                if (packageType != AntPackagingType.AnimationSet)
                {
                    bank.PackageMeta = LoadPackageMeta(reader);
                }

                if(bank.PackageMeta.ExtraBytes > 0)
                {
                    uint guidIntCount = bank.PackageMeta.StaticGuidToIntSize / 0x14;
                    for (int i = 0; i < guidIntCount; i++)
                    {
                        bank.StaticGuidToKeyMap.Add(reader.ReadGuid(), reader.ReadUInt32());
                    }

                    for (int i = 0; i < bank.PackageMeta.StaticBundleImportCountInSlots; i++)
                    {
                        AntImportNode node = new AntImportNode() { Key = reader.ReadUInt32(), Tid = reader.ReadUInt64() };
                        bank.ImportNodes.Add(node);
                    }
                }

                LoadStream(reader, out List<ReflLayoutData> assets, out ReflLayoutCollection layouts);
                bank.AssetData = assets;
                bank.Layouts = layouts;

                return bank;
            }
        }

        public static void LoadStream(Stream stream, out List<ReflLayoutData> assetData, out ReflLayoutCollection layouts)
        {
            using (ExtendedBinaryReader reader = new ExtendedBinaryReader(stream))
            {
                LoadStream(reader, out assetData, out layouts);
            }
        }

        public static void LoadStream(ExtendedBinaryReader reader, out List<ReflLayoutData> assetData, out ReflLayoutCollection layouts)
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
                
            layouts = LoadREFLBlob(reader, header);
            
            assetData = new List<ReflLayoutData>();

            GenericDataBlobReader blobReader = new GenericDataBlobReader(reader, layouts);
            while (reader.Position < strmEndOffset)
            {
                header = GenericDataHeader.LoadHeader(reader);
                if (header.Format != GenericDataFormat.GD_DATA)
                    throw new InvalidDataException($"Unsupported blob format: {header.Format}");

                var dataBlob = LoadDATABlob(blobReader, header);
                assetData.Add(dataBlob);
            }
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
            }

            return meta;
        }
        
        #endregion

        #region Saving methods

        public static void SaveBank(Stream stream, AntAssetBank bank, Endian endian)
        {
            using(ExtendedBinaryWriter writer = new ExtendedBinaryWriter(stream, true))
            {
                writer.Endianness = Endian.Big;
                writer.WriteUInt32((uint)bank.PackageMeta.PackageType);
                if(bank.PackageMeta.PackageType != AntPackagingType.AnimationSet)
                {
                    writer.WriteUInt32(bank.PackageMeta.MetaBytes);
                    writer.WriteUInt32((uint)bank.PackageMeta.PackageType);
                    writer.WriteUInt32(bank.PackageMeta.AssetCount);
                    writer.WriteUInt32(bank.PackageMeta.ImportCount);
                    writer.WriteUInt32(bank.PackageMeta.AssetBytes);
                    writer.WriteUInt32(bank.PackageMeta.ExtraBytes);

                    if(bank.PackageMeta.ExtraBytes > 0)
                    {
                        writer.WriteUInt32(bank.PackageMeta.TotalNonStaticAssetCount);
                        writer.WriteUInt32(bank.PackageMeta.MaxNonStaticAssetCount);
                        writer.WriteUInt32(bank.PackageMeta.MaxNonStaticImportCount);
                        writer.WriteUInt32(bank.PackageMeta.StaticBundleImportBucketSize);
                        writer.WriteUInt32(bank.PackageMeta.StaticBundleImportBucketEntries);
                        writer.WriteUInt32(bank.PackageMeta.StaticBundleFlatImportCount);
                        writer.WriteUInt32(bank.PackageMeta.StaticBundleImportCountInSlots);
                        writer.WriteUInt32(bank.PackageMeta.StaticGuidToIntSize);

                        foreach(var kvp in bank.StaticGuidToKeyMap)
                        {
                            writer.WriteGuid(kvp.Key);
                            writer.WriteUInt32(kvp.Value);
                        }

                        foreach(var import in bank.ImportNodes)
                        {
                            writer.WriteUInt32(import.Key);
                            writer.WriteUInt64(import.Tid);
                        }
                    }
                }

                SaveStream(stream, endian, bank.AssetData, bank.Layouts);
            }
        }

        public static void SaveStream(Stream stream, Endian endian, List<ReflLayoutData> data, ReflLayoutCollection layouts)
        {
            GenericDataBlobWriter blobWriter = new GenericDataBlobWriter(stream, endian);
            blobWriter.BeginBlob(GenericDataFormat.GD_STRM);
            blobWriter.WriteUInt32(DataUtil.PTR_PLACEHOLDER);

            SaveREFL(stream, endian, layouts);

            int biggestBlobSize = 0;
            foreach (var asset in data)
            {
                int dataBlobSize = SaveData(stream, endian, asset);
                biggestBlobSize = Math.Max(biggestBlobSize, dataBlobSize);
            }

            blobWriter.Position = 0xC;
            // not sure why exactly they do this
            uint strmBiggestBlobSize = (uint)(2 * (5 * biggestBlobSize + 640) / 9u);
            blobWriter.WriteUInt32(strmBiggestBlobSize);

            blobWriter.EndBlob();
        }
        
        public static int SaveREFL(Stream stream, Endian endian, ReflLayoutCollection layouts)
        {
            GenericDataBlobWriter blobWriter = new GenericDataBlobWriter(stream, endian);
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
            
            relocTable.WriteRelocTable(blobWriter);
            
            return blobWriter.EndBlob();
        }

        public static int SaveData(Stream stream, Endian endian, ReflLayoutData data)
        {
            GenericDataBlobWriter blobWriter = new GenericDataBlobWriter(stream, endian);
            blobWriter.BeginBlob(GenericDataFormat.GD_DATA);

            List<ReflLayoutData> dataSet = new List<ReflLayoutData>();
            GatherData(data, dataSet);

            if(dataSet.Count > 0)
            {
                blobWriter.WriteUInt32(DataUtil.PTR_PLACEHOLDER); // reloc table offset

                RelocationTable relocTable = new RelocationTable(blobWriter.Position);

                foreach(var dat in dataSet)
                {
                    dat.Save(blobWriter, relocTable);
                }
                blobWriter.Position = blobWriter.Length;

                relocTable.WriteRelocTable(blobWriter);
            }
            
            return blobWriter.EndBlob();
        }

        public static void GatherData(ReflLayoutData data, List<ReflLayoutData> dataSet)
        {
            if (data != null && !dataSet.Contains(data))
            {
                dataSet.Add(data);

                foreach (var entry in data.Layout.Entries)
                {
                    if (entry.FieldCategory != ReflFieldCategory.Invalid)
                        GatherData(entry, data.ValueByName[entry.FixedName], dataSet);
                }
            }
        }

        public static void GatherData(ReflLayout.FieldEntry entry, object value, List<ReflLayoutData> dataSet)
        {
            if (entry.LayoutHash == ReflLayoutHash.Invalid)
                return;

            if (entry.FieldCategory == ReflFieldCategory.Nested ||
                entry.FieldCategory == ReflFieldCategory.ListNested)
                return;

            bool isArray = (entry.Flags & ReflLayoutFlags.Array) != 0;
            int count = entry.Count;

            if (count == 1 && isArray)
            {
                count = value is IList list
                    ? list.Count
                    : ((Array)value).Length;
            }

            ReflLayoutHash entryHash = entry.Layout.LayoutHash;
            bool isNested = entryHash > ReflLayoutHash.Key;
            bool isDataRef = entryHash == ReflLayoutHash.DataRef;

            if (count > 1 && !isArray)
            {
                if (isNested || isDataRef)
                {
                    throw new NotImplementedException();

                    //foreach (var entry in entry.Layout.Entries)
                }

                return;
            }

            if (isArray)
            {
                if (isNested || isDataRef)
                {
                    if (value is IEnumerable enumerable)
                    {
                        foreach (object element in enumerable)
                        {
                            if (element is ReflLayoutData data)
                                GatherData(data, dataSet);
                        }
                    }
                }

                return;
            }

            if (isDataRef)
            {
                GatherData(value as ReflLayoutData, dataSet);
                return;
            }

            if (isNested)
            {
                throw new NotImplementedException();
                //foreach (var entry in entry.Layout.Entries)
            }
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
                    CollectLayoutTypes((ReflLayoutData)data.ValueByName[entry.FixedName], reflLayouts);
                }
                else if (entry.Layout == ReflLayoutType.DataRef || !entry.Layout.IsNativeType)
                {
                    CollectLayoutTypes(entry.Layout, reflLayouts);
                }
            }
        }
        
        #endregion
        
        // Verify that serialization works correctly by rewriting all input data
        public static void TestSerialization(List<ReflLayoutData> data, ReflLayoutCollection layouts, Endian endian)
        {
            DebuggableMemoryStream strm = new DebuggableMemoryStream();
            ReflLayoutCollection layoutTypes = new ReflLayoutCollection();
            ExtendedBinaryReader testBR = new ExtendedBinaryReader(strm);
            
            GenericDataHeader header;
            for (int i = 21010; i < data.Count; i++)
            {
                ReflLayoutData asset = data[i];
                CollectLayoutTypes(asset, layoutTypes);
                long blobPos = strm.Position;
                //try
                {
                    SaveData(strm, endian, asset);
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