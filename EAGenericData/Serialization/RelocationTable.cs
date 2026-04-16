using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using EAGenericData.IO;

namespace EAGenericData.Serialization
{
	public struct Relocation
	{
		public long Offset;
	}
	
    public sealed class RelocationTable
    {
		private static readonly byte[] s_emptyBytes = new byte[16];

		private readonly long m_startOffset;

		private Dictionary<object, long> m_registeredObjects = new Dictionary<object, long>();
		private SortedDictionary<long, object> m_localObjects = new SortedDictionary<long, object>();
		
		private Dictionary<ulong, long> m_registeredPtrs = new Dictionary<ulong, long>();
		private Dictionary<long, ulong> m_localPtrs = new Dictionary<long, ulong>();
		
		public IEnumerable<object> LocalObjects => m_localObjects.Values;

		public RelocationTable(long startPos)
		{
			m_startOffset = startPos;
		}

		public void Clear()
		{
			m_registeredObjects.Clear();
			m_localObjects.Clear();
			m_registeredPtrs.Clear();
			m_localPtrs.Clear();
		}

		public void RegisterPtr(long offset, ulong ptr)
		{
			m_registeredPtrs.Add(ptr, offset);
		}

		public void RegisterObject(long offset, object obj)
		{
			m_registeredObjects.Add(obj, offset);
		}

		public bool IsObjectRegistered(object aPointer)
		{
			return m_registeredObjects.ContainsKey(aPointer);
		}

		public bool IsPointerRegistered(ulong ptr)
		{
			return m_registeredPtrs.ContainsKey(ptr);
		}

		public Relocation RelocPtr(long aOffset, ulong ptr)
		{
			if (ptr == 0) return new Relocation { Offset = 0 };
			
			m_localPtrs.Add(aOffset, ptr);
			return new Relocation { Offset = DataUtil.PTR_PLACEHOLDER_LONG };
		}

		public Relocation RelocObject(long offset, object obj)
		{
			if (obj == null) return new Relocation { Offset = 0 };
			
			m_localObjects.Add(offset, obj);
			return new Relocation { Offset = DataUtil.PTR_PLACEHOLDER_LONG };
		}
		
		public void WriteRelocTable(IDataWriter writer)
		{
            AlignWriter(writer, 4);
            long relocTableOffset = writer.Position;
            writer.Position = 0xC;
            writer.WriteUInt32((uint)relocTableOffset);
            writer.Position = relocTableOffset;

            int count = m_localObjects.Count > 0 ? m_localObjects.Count : m_localPtrs.Count;
            writer.WriteUInt32((uint)count);

            if (m_localObjects.Count > 0)
			{
				Debug.Assert(m_localPtrs.Count == 0);
				foreach (var kvp in m_localObjects)
				{
					writer.WriteUInt32((uint)(kvp.Key - m_startOffset));
				}
			}

			if (m_localPtrs.Count > 0)
			{
				Debug.Assert(m_localObjects.Count == 0);
				foreach (var kvp in m_localPtrs)
				{
					writer.WriteUInt32((uint)(kvp.Key - m_startOffset));
				}
			}
			FixupOffsets(writer);
			Clear();
		}

		public void FixupOffsets(IDataWriter writer)
		{
			long curPos = writer.Position;
			
			if (m_localObjects.Count > 0)
			{
				Debug.Assert(m_localPtrs.Count == 0);
				foreach (var kvp in m_localObjects)
				{
					if (!m_registeredObjects.TryGetValue(kvp.Value, out var value))
						throw new InvalidDataException($"Unresolved object pointer at 0x{kvp.Key:X}");
					
					writer.Position = kvp.Key;
					writer.WriteUInt64((ulong)(value - m_startOffset));
				}
			}
			
			if (m_localPtrs.Count > 0)
			{
				Debug.Assert(m_localObjects.Count == 0);
				foreach (var kvp in m_localPtrs)
				{
					if (!m_registeredPtrs.TryGetValue(kvp.Value, out var value2))
						throw new InvalidDataException($"Unresolved pointer at 0x{kvp.Key:X}");
					
					writer.Position = kvp.Key;
					writer.WriteUInt64((ulong)(value2 - m_startOffset));
				}
			}
			
			writer.Position = curPos;
		}

		public long AlignWriter(IDataWriter writer, int alignment)
		{
			long relativePos = writer.Position - m_startOffset;
			long alignedPos = DataUtil.Align(relativePos, alignment);
			long padding = alignedPos - relativePos;

			if (padding <= 0)
				return writer.Position;

			if (padding < 16)
			{
				writer.Write(s_emptyBytes, 0, (int)padding);
			}
			else
			{
				writer.Write(new byte[padding]);
			}

			return writer.Position;
		}

		public long AlignPosition(long offset, int alignment)
		{
			long relative = offset - m_startOffset;
			long aligned = DataUtil.Align(relative, alignment);
			return aligned + m_startOffset;
		}
	}
}