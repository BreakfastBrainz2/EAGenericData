using EAGenericData.Layout;
using System;
using System.Collections.Generic;

namespace EAGenericData.Frostbite
{
    public struct AntImportNode
    {
        public uint Key;
        public ulong Tid;

        public override string ToString()
        {
            return $"{Key} - {Tid}";
        }
    }

    public struct AssetKey
    {
        public const uint KeyStaticFlag = 0x00080000;
        public const uint KeyValueMask = ~KeyStaticFlag;

        private static readonly byte[] s_emptyBytes = new byte[8];

        public uint Key => m_key;
        public uint Id
        {
            get => m_key & KeyValueMask;
            set { m_key = (m_key & KeyStaticFlag) | (value & KeyValueMask); }
        }

        public bool IsStatic
        {
            get => (m_key & KeyStaticFlag) != 0;
            set
            {
                if(value)
                    m_key |= KeyStaticFlag;
                else
                    m_key &= ~KeyStaticFlag;
            }
        }

        private uint m_key;

        public AssetKey(uint id, bool isStatic = false)
        {
            m_key = id &= KeyValueMask;

            if (isStatic)
                m_key |= KeyStaticFlag;
        }

        public AssetKey(Guid guid)
        {
            var bytes = guid.ToByteArray();

            m_key = (uint)(bytes[0]
                | (bytes[1] << 8)
                | (bytes[2] << 16)
                | (bytes[3] << 24));
        }

        public Guid ToGuid()
        {
            return ToGuid(m_key);
        }

        public static Guid ToGuid(uint key)
        {
            return new Guid(
                (int)key,
                0,
                0,
                s_emptyBytes
            );
        }
    }

    public class AntAssetBank
    {
        public StaticAntPackageMeta PackageMeta { get; set; }

        public ReflLayoutCollection Layouts { get; set; } = new ReflLayoutCollection();
        public List<ReflLayoutData> AssetData { get; set; } = new List<ReflLayoutData>();
        public Dictionary<Guid, uint> StaticGuidToKeyMap = new Dictionary<Guid, uint>();
        public List<AntImportNode> ImportNodes = new List<AntImportNode>();

        public Dictionary<Guid, ReflLayoutData> GuidToAssetMap = new Dictionary<Guid, ReflLayoutData>();
    }
}