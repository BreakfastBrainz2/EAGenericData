using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using EAGenericData.IO;
using EAGenericData.Serialization;

namespace EAGenericData.Layout
{
    public class ReflLayout
    {
	    private const int LAYOUT_ENTRY_SIZE = 32;
	    
        public class FieldEntry
        {
            public int FieldId;
            public ReflLayout Layout;
            public ReflLayoutFlags Flags;
            public ReflFieldCategory FieldCategory;
            public string Name;
            public ReflLayoutHash LayoutHash;
            public int NameHash;
            public int RunLengthEncoding;
            public short Offset;
            public ushort Count;
            public short NameIdx;
            public short ElementSize;
            public ushort ElementAlign;
        }
        
        #region Sorters
        
        private class SortByAlignmentAndSize : IComparer<FieldEntry>
        {
	        public int Compare(FieldEntry first, FieldEntry second)
	        {
		        int alignmentFirst = GetFieldAlignment(first);
		        int alignmentSecond = GetFieldAlignment(second);
		        
		        if (alignmentFirst > alignmentSecond)
			        return -1;

		        if (alignmentFirst != alignmentSecond)
			        return 1;
		        
		        if (first.ElementSize > second.ElementSize)
			        return -1;

		        if (first.ElementSize != second.ElementSize)
			        return 1;
		        
		        if (first.Count > second.Count)
			        return -1;

		        if (first.Count != second.Count)
			        return 1;
		        
		        if (first.LayoutHash < second.LayoutHash)
			        return -1;
		        
		        return first.LayoutHash == second.LayoutHash
			        ? Math.Sign(first.RunLengthEncoding - second.RunLengthEncoding)
			        : 1;
	        }
        }
        
        private class SortById : IComparer<FieldEntry>
        {
	        public int Compare(FieldEntry first, FieldEntry second)
	        {
		        return Math.Sign(first.RunLengthEncoding - second.RunLengthEncoding);
	        }
        }
        
        #endregion
        
        public string Name { get; private set; }
        public int MinSlot { get; private set; }
        public int MaxSlot { get; private set; }
        public int DataSize { get; private set; }
        public int Alignment { get; private set; }
        public bool IsReordered { get; private set; }
        public bool IsPod { get; private set; }
        public ReflLayoutHash LayoutHash { get; private set; }

        public int NumEntries { get; private set; }
        public FieldEntry[] Entries { get; private set; }
        public FieldEntry[] ValidEntries { get; private set; }

        public ReflStringTable StringTable { get; private set; } = new ReflStringTable();
        public bool IsNativeType => ReflLayoutType.FromHash(LayoutHash) != null;
        
        private static readonly IComparer<FieldEntry> s_sortByAlignAndSize = new SortByAlignmentAndSize();
        private static readonly IComparer<FieldEntry> s_sortById = new SortById();

        internal ReflLayout() { }

        public ReflLayout(string name, bool reorder, IList<ReflLayoutField> entries)
        {
	        Init(name, entries, reorder, 1, !reorder);
	        LayoutHash = CalculateHash();
        }
        
        public ReflLayout(string name, int size, int align, ReflLayoutHash layoutHash, IList<ReflLayoutField> entries)
        {
	        Init(name, entries, false, align, false);
	        DataSize = size;
	        Alignment = align;
	        LayoutHash = layoutHash;
        }
        
        private static int GetFieldAlignment(FieldEntry fieldEntry)
        {
	        int alignment = fieldEntry.ElementAlign;
	        
	        if ((fieldEntry.Flags & ReflLayoutFlags.ForceAlign4) == ReflLayoutFlags.ForceAlign4)
		        return Math.Max(4, alignment);
	        
	        if ((fieldEntry.Flags & ReflLayoutFlags.ForceAlign8) == ReflLayoutFlags.ForceAlign8)
		        return Math.Max(8, alignment);
	        
	        if ((fieldEntry.Flags & ReflLayoutFlags.ForceAlign16) == ReflLayoutFlags.ForceAlign16)
		        return Math.Max(16, alignment);
	        
	        return alignment;
        }
        
        private static int GetFieldSize(FieldEntry fieldEntry)
        {
	        if (fieldEntry.Count <= 1)
		        return fieldEntry.ElementSize;
	        
	        return DataUtil.ArraySize(fieldEntry.ElementSize, fieldEntry.ElementAlign, fieldEntry.Count);
        }
        
        public ReflLayoutHash CalculateHash()
        {
	        uint result = 0;
	        foreach (var entry in Entries)
	        {
		        result = Crc32.Hash((uint)entry.LayoutHash, result);
		        result = Crc32.Hash((uint)entry.NameIdx, result);
		        result = Crc32.Hash(entry.Count, result);
		        result = Crc32.Hash((ushort)entry.Flags, result);
		        result = Crc32.Hash((int)entry.Offset, result);
		        result = Crc32.Hash((short)entry.RunLengthEncoding, result);
	        }

	        var tableBytes = StringTable.ToByteArray();
	        result = Crc32.HashArray(tableBytes, result);
	        result = Crc32.Hash(MinSlot, result);
	        result = Crc32.Hash(MaxSlot, result);
	        return (ReflLayoutHash)Crc32.Hash(Alignment, result);
        }
        
        private void Init(string name, IList<ReflLayoutField> userFields, bool isReordered, int minAlign, bool shouldPadToAlign)
		{
		    Alignment = Math.Max(1, minAlign);
		    IsReordered = isReordered;
		    
		    MinSlot = 0;
		    MaxSlot = -1;

		    foreach (var entry in userFields)
		    {
		        MinSlot = Math.Min(MinSlot, entry.Id);
		        MaxSlot = Math.Max(MaxSlot, entry.Id);
		    }

		    NumEntries = (MaxSlot - MinSlot) + 1;
		    
		    var fieldEntries = new FieldEntry[NumEntries];
		    for (int i = 0; i < NumEntries; i++)
		    {
		        fieldEntries[i] = new FieldEntry
		        {
		            RunLengthEncoding = i - MinSlot
		        };
		    }
		    
		    foreach (var userField in userFields)
		    {
		        if (userField.Layout == null)
			        throw new InvalidDataException($"Field {userField.Id}: {userField.Name} has no layout assigned. Fields are required to have a layout.");

		        int index = userField.Id - MinSlot;
		        FieldEntry fieldEntry = fieldEntries[index];

		        if (fieldEntry.Layout != null)
			        throw new InvalidDataException($"Duplicated field: Field {userField.Id} {userField.Name} in layout {name}.");

		        fieldEntry.FieldId = userField.Id;
		        fieldEntry.Layout = userField.Layout;
		        fieldEntry.LayoutHash = userField.Layout.LayoutHash;
		        fieldEntry.Offset = 0;
		        fieldEntry.NameIdx = 0;
		        fieldEntry.Count = (ushort)userField.Count;
		        fieldEntry.Flags = userField.Flags;
		        fieldEntry.FieldCategory = CalcCategory(userField.Layout, userField.Flags, userField.Count);

		        if ((userField.Flags & ReflLayoutFlags.Array) != 0)
		        {
		            fieldEntry.ElementSize = DataUtil.GD_ARRAY_SIZE;
		            fieldEntry.ElementAlign = DataUtil.GD_ARRAY_ALIGN;
		        }
		        else
		        {
		            fieldEntry.ElementSize = (short)userField.Layout.DataSize;
		            fieldEntry.ElementAlign = (ushort)userField.Layout.Alignment;
		        }
		    }
		    
		    if (isReordered)
		        Array.Sort(fieldEntries, s_sortByAlignAndSize);

		    foreach (FieldEntry fld in fieldEntries)
		    {
			    int align = GetFieldAlignment(fld);
			    int size = GetFieldSize(fld);

			    if (align <= 0 || size <= 0)
				    continue;

			    DataSize = DataUtil.Align(DataSize, align);
			    fld.Offset = (short)DataSize;
			    DataSize += size;

			    Alignment = Math.Max(Alignment, align);   
		    }

		    if (isReordered)
		        Array.Sort(fieldEntries, s_sortById);

		    if (shouldPadToAlign)
		        DataSize = DataUtil.Align(DataSize, Alignment);
		    
		    StringTable.Add("");
		    StringTable.Add(name);
		    
		    for (int i = 0; i < NumEntries; i++)
		        fieldEntries[i].RunLengthEncoding = -1;

		    for (int i = 0; i < userFields.Count; i++)
		    {
		        int index = userFields[i].Id - MinSlot;
		        fieldEntries[index].RunLengthEncoding = i;
		    }
		    
		    for (int i = 0; i < NumEntries; i++)
		    {
		        FieldEntry entry = fieldEntries[i];
		        int rle = entry.RunLengthEncoding;

		        if (rle >= 0)
		        {
		            string entryName = userFields[rle].Name;

		            if (!string.IsNullOrEmpty(entryName))
		            {
		                entry.NameIdx = (short)StringTable.Add(entryName);
		                entry.Name = entryName;
		                entry.NameHash = entryName.GetHashCode();
		            }
		        }

		        entry.RunLengthEncoding = entry.Count;
		    }
		    
		    // calculate RLE of fields
		    for (int i = NumEntries - 2; i >= 0; i--)
		    {
		        FieldEntry current = fieldEntries[i];
		        FieldEntry next = fieldEntries[i + 1];

		        if (current.LayoutHash == next.LayoutHash && current.Flags == next.Flags)
		        {
		            current.RunLengthEncoding += next.RunLengthEncoding;
		        }
		    }

		    Entries = fieldEntries;
		    Name = name;

		    CollectValidEntries();
		}

		public override string ToString()
		{
			return Name;
		}

		public override int GetHashCode()
		{
			return (int)LayoutHash;
		}

		public override bool Equals(object obj)
		{
			ReflLayout layout = obj as ReflLayout;
			if (layout == null)
			{
				return base.Equals(obj);
			}

			return Equals(layout);
		}

		public bool Equals(ReflLayout other)
		{
			if (other == null)
				return false;

			return other.LayoutHash == LayoutHash;
		}

		// some layouts have empty entries (editor fields?), so we filter out any empty entries here.
		private void CollectValidEntries()
		{
			int numInvalid = Entries.Count(x => x.LayoutHash == ReflLayoutHash.Invalid);
			
			if (numInvalid == 0)
			{
				ValidEntries = Entries;
				return;
			}
			
			ValidEntries = new FieldEntry[NumEntries - numInvalid];
			
			int i = 0;
			foreach (var entry in Entries)
			{
				if (entry.LayoutHash != ReflLayoutHash.Invalid)
				{
					ValidEntries[i] = entry;
					i++;
				}
			}
		}

        public void Load(ExtendedBinaryReader reader, SortedList<long, ReflLayout> layoutPtrTable)
        {
            long beginOffset = reader.BaseStream.Position;
            
            MinSlot = reader.ReadInt32();
            MaxSlot = reader.ReadInt32();
            DataSize = reader.ReadInt32();
            Alignment = reader.ReadInt32();

            uint stringTableOffset = reader.ReadUInt32();
            int stringTableSize = reader.ReadInt32();

            IsReordered = reader.ReadBoolean();
            IsPod = reader.ReadBoolean();

            reader.BaseStream.Position += 2; // pad bytes

            LayoutHash = (ReflLayoutHash)reader.ReadUInt32();
            NumEntries = MaxSlot - MinSlot + 1;
            Entries = new FieldEntry[NumEntries];

            for (int i = 0; i < NumEntries; i++)
            {
                Entries[i] = new FieldEntry
                {
                    LayoutHash = (ReflLayoutHash)reader.ReadUInt32(),
                    ElementSize = (short)reader.ReadInt32(),
                    Offset = (short)reader.ReadInt32(),
                    NameIdx = (short)reader.ReadInt32(),
                    Count = reader.ReadUInt16(),
                    Flags = (ReflLayoutFlags)reader.ReadUInt16(),
                    ElementAlign = reader.ReadUInt16(),
                    RunLengthEncoding = reader.ReadInt16(),
                    FieldId = MinSlot + i
                };

                long layoutPtr = reader.ReadInt64();
                if (layoutPtr != 0)
                {
                    Entries[i].Layout = layoutPtrTable[layoutPtr];
                }
            }

            StringTable = new ReflStringTable(reader, stringTableSize);
            foreach (var entry in Entries)
            {
	            entry.Name = StringTable.GetByOffset(entry.NameIdx);
	            entry.NameHash = entry.Name.GetHashCode();
            }
            
            Name = StringTable.GetByIndex(1);

            CollectValidEntries();
            reader.BaseStream.Position = beginOffset + stringTableOffset + stringTableSize;
        }

        public void Save(IDataWriter writer, RelocationTable relocTable)
        {
	        long beginPos = relocTable.AlignWriter(writer, 8);
	        
	        relocTable.RegisterPtr(beginPos, (ulong)LayoutHash);

	        int entryTableSize = NumEntries * LAYOUT_ENTRY_SIZE;
	        int strTablePos = LAYOUT_ENTRY_SIZE + entryTableSize;
	        
	        writer.WriteInt32(MinSlot);
	        writer.WriteInt32(MaxSlot);
	        writer.WriteInt32(DataSize);
	        writer.WriteInt32(Alignment);
	        writer.WriteInt32(strTablePos);
	        writer.WriteInt32(StringTable.GetTableSize());
	        writer.WriteBool(IsReordered);
	        writer.WriteBool(IsPod);
	        writer.WriteUInt16(0); // pad
	        writer.WriteUInt32((uint)LayoutHash);
	        
	        Debug.Assert((writer.Position - beginPos) == LAYOUT_ENTRY_SIZE);

	        foreach (var entry in Entries)
	        {
		        writer.WriteUInt32((uint)entry.LayoutHash);
		        writer.WriteInt32(entry.ElementSize);
		        writer.WriteInt32(entry.Offset);
		        writer.WriteInt32(entry.NameIdx);
		        writer.WriteUInt16(entry.Count);
		        writer.WriteUInt16((ushort)entry.Flags);
		        writer.WriteUInt16(entry.ElementAlign);
		        writer.WriteInt16((short)entry.RunLengthEncoding);
		        writer.WriteReloc(relocTable.RelocPtr(writer.Position, (ulong)entry.LayoutHash));
	        }
	        
	        Debug.Assert((writer.Position - beginPos) == strTablePos);
	        writer.Write(StringTable.ToByteArray());
        }

        public ReflFieldCategory CalcCategory(ReflLayout layout, ReflLayoutFlags flags, int elementCount)
        {
            if (layout == ReflLayoutType.Invalid || elementCount <= 0)
            {
                return ReflFieldCategory.Invalid;
            }

            if (elementCount > 1)
            {
                return ReflFieldCategory.Array | CalcCategory(layout, flags, 1);
            }

            if ((flags & ReflLayoutFlags.Array) == ReflLayoutFlags.Array)
            {
                return ReflFieldCategory.List | CalcCategory(layout, ReflLayoutFlags.None, 1);
            }

            // is this a simple type?
            if (ReflLayoutType.FromHash(layout.LayoutHash) != null)
            {
                return ReflFieldCategory.Value;
            }

            return ReflFieldCategory.Nested;
        }

        public void PostLoad()
        {
            foreach (var entry in Entries)
            {
                entry.FieldCategory = CalcCategory(entry.Layout, entry.Flags, entry.Count);
            }
        }
        
        private string FixupName()
        {
	        if (string.IsNullOrEmpty(Name))
	        {
		        return $"__UnnamedLayout_{LayoutHash:X}";
	        }
	        
	        return Name.Replace(":", "_").Replace(" ", "_");
        }

        public string DumpInfo()
        {
	        StringBuilder sb = new StringBuilder();

	        sb.AppendLine($"Layout: {FixupName()} ({LayoutHash:X}) size=0x{DataSize:X} align={Alignment}");
	        foreach (var fld in ValidEntries)
	        {
		        string fieldName = fld.Name;
		        if ((fld.Flags & ReflLayoutFlags.Array) == ReflLayoutFlags.Array)
			        fieldName += "[]";
		        
		        sb.AppendLine(
			        $"	Field {fld.FieldId}: {fld.Layout.FixupName()} {fieldName} at 0x{fld.Offset:X} size=0x{fld.ElementSize:X} align={fld.ElementAlign} flags={fld.Flags}");
	        }

	        return sb.ToString();
        }
    }
}