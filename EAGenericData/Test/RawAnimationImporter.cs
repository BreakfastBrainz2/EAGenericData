using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using EAGenericData.Layout;
using Assimp;
using EAGenericData.Frostbite;

namespace EAGenericData.Test
{
    public enum BoneChannelType
    {
        None = 0,
        Quaternion = 14,
        Translation = 2049856663,
        Scale = 2049856454,
    }

    public enum DofStorageType
    {
        Invalid,
        Raw8,
        Raw16
    }

    public enum FrostbiteVersion
    {
        GW1 = 201320,
        GW2 = 2014411
    }

    public struct BoneLayout
    {
        public string Name;
        public BoneChannelType Type;

        public override bool Equals(object obj)
        {
            if (obj is BoneLayout lyt)
            {
                return Name.Equals(lyt.Name) && Type == lyt.Type;
            }

            return false;
        }

        public override string ToString()
        {
            return $"{Name}: {Type}";
        }
    }

    public struct DofChannel
    {
        public BoneLayout Layout;
        public ushort Dof;

        public override string ToString()
        {
            return $"{Layout.Name}: {Layout.Type}";
        }
    }

    public class ChannelToDof
    {
        public List<DofChannel> Channels { get; private set; } = new List<DofChannel>();

        public ReflLayoutData BuildMappingAsset(AntAssetBank bank, FrostbiteVersion version)
        {
            var channelToDofAsset = bank.CreateAssetOfType("ChannelToDofAsset", "ChannelToDofSetVirtualAsset");
            switch (version)
            {
                case FrostbiteVersion.GW1:
                {
                    bool needsShortIndices = Channels.Count(x => x.Dof > byte.MaxValue) > 0;
            
                    int numDofIndices = needsShortIndices
                        ? Channels.Count * 2
                        : Channels.Count;
                    
                    channelToDofAsset.SetValue("StorageType", (uint)(needsShortIndices ? DofStorageType.Raw16 : DofStorageType.Raw8));
            
                    List<byte> dofIndices = new List<byte>(numDofIndices);

                    if (needsShortIndices)
                    {
                        foreach (var channel in Channels)
                        {
                            dofIndices.Add((byte)(channel.Dof >> 8));
                            dofIndices.Add((byte)(channel.Dof & 0xFF));
                        }
                    }
                    else
                    {
                        foreach (var channel in Channels)
                        {
                            Debug.Assert(channel.Dof <= byte.MaxValue);
                            dofIndices.Add((byte)channel.Dof);
                        }
                    }
                    
                    channelToDofAsset.SetValue("IndexData", dofIndices);
                    return channelToDofAsset;
                }
                case FrostbiteVersion.GW2:
                { 
                    var dofIndices = new List<ushort>();
                    foreach (var channel in Channels)
                    {
                        dofIndices.Add(channel.Dof);
                    }
                    
                    channelToDofAsset.SetValue("DofIds", dofIndices);
                    return channelToDofAsset;
                }
                default: throw new NotImplementedException();
            }
        }
    }

    public abstract class AnimationImporter
    {
        protected AntAssetBank m_bank;
        protected ReflLayoutData m_rigAsset;
        protected ReflLayoutData m_layoutHierarchyAsset;
        protected FrostbiteVersion m_fbVersion;

        protected AnimationImporter(AntAssetBank bank, ReflLayoutData rig, ReflLayoutData layoutHierarchy, FrostbiteVersion version)
        {
            m_bank = bank;
            m_rigAsset = rig;
            m_layoutHierarchyAsset = layoutHierarchy;
            m_fbVersion = version;
        }

        public abstract void ImportAnimation(Animation animation, ReflLayoutData animData);
        
        protected Quaternion GetQuatKey(double timeInTicks, List<QuaternionKey> keys)
        {
            if (keys.Count == 0)
                return Quaternion.Identity;

            if (keys.Count == 1)
                return keys[0].Value;

            if (timeInTicks <= keys[0].Time)
                return keys[0].Value;

            if (timeInTicks >= keys[keys.Count - 1].Time)
                return keys[keys.Count - 1].Value;
            
            for (int i = 0; i < keys.Count - 1; i++)
            {
                var k1 = keys[i];
                var k2 = keys[i + 1];

                if (timeInTicks < k2.Time)
                {
                    double delta = k2.Time - k1.Time;
                    float factor = (float)((timeInTicks - k1.Time) / delta);

                    return Quaternion.Slerp(k1.Value, k2.Value, factor);
                }
            }

            throw new InvalidDataException();
        }
        
        protected Vector3 GetVectorKey(double timeInTicks, List<VectorKey> keys)
        {
            if (keys.Count == 0)
                return Vector3.Zero;

            if (keys.Count == 1)
                return keys[0].Value;

            if (timeInTicks <= keys[0].Time)
                return keys[0].Value;

            if (timeInTicks >= keys[keys.Count - 1].Time)
                return keys[keys.Count - 1].Value;
            
            for (int i = 0; i < keys.Count - 1; i++)
            {
                var k1 = keys[i];
                var k2 = keys[i + 1];

                if (timeInTicks < k2.Time)
                {
                    double delta = k2.Time - k1.Time;
                    float factor = (float)((timeInTicks - k1.Time) / delta);

                    return Vector3.Lerp(k1.Value, k2.Value, factor);
                }
            }

            throw new InvalidDataException();
        }
        
        protected Dictionary<string, DofChannel> CreateDofLookupMap(ReflLayoutData lytHierarchy)
        {
            var channelLookup = new Dictionary<string, DofChannel>();

            switch (m_fbVersion)
            {
                case FrostbiteVersion.GW1:
                {
                    ushort dofIdx = 0;
                    foreach (var lytGuid in lytHierarchy.GetValue<List<Guid>>("LayoutAssets"))
                    {
                        var layoutAsset = m_bank.GuidToAssetMap[lytGuid];

                        if (layoutAsset.Layout.Name == "LayoutAsset")
                        {
                            foreach (var layoutEntry in layoutAsset.GetValue<List<ReflLayoutData>>("Slots"))
                            {
                                string name = layoutEntry.GetValue<string>("Name");
                                uint type = layoutEntry.GetValue<uint>("Type");

                                DofChannel channel = new DofChannel
                                { 
                                    Dof = dofIdx++, 
                                    Layout = new BoneLayout { Name = name, Type = (BoneChannelType)type }
                                };
                                channelLookup.Add(name, channel);
                                //channelNames.Add(new BoneLayout{Name = name, Type = (BoneChannelType)type});
                            }
                        }
                        else
                        {
                            Debug.Assert(layoutAsset.Layout.Name == "DeltaTrajLayoutAsset");
                            for (int i = 0; i < 8; i++)
                            {
                                //channelNames.Add(new BoneLayout { Name = i.ToString(), Type = BoneChannelType.Quaternion });
                                DofChannel channel = new DofChannel
                                { 
                                    Dof = dofIdx++,
                                    Layout = new BoneLayout { Name = i.ToString(), Type = BoneChannelType.Quaternion }
                                };
                                channelLookup.Add(i.ToString(), channel);
                            }
                        }
                    }

                    break;
                }
                case FrostbiteVersion.GW2:
                {
                    var rigDofSets = m_rigAsset.GetValue<List<Guid>>("RigDofSets");
                    var rigDofSetIdIndices = m_rigAsset.GetValue<List<ushort>>("DofSetIdIndices");
                    var rigDofIds = m_rigAsset.GetValue<List<ushort>>("DofIds");
                
                    for (int setIt = 0; setIt < rigDofSets.Count; ++setIt)
                    {
                        var dofSet = m_bank.GuidToAssetMap[rigDofSets[setIt]];
                        if (dofSet.Layout.Name != "LayoutAsset")
                        {
                            Debug.Assert(dofSet.Layout.Name == "DeltaTrajLayoutAsset");
                            continue;
                        }

                        var idOffset = rigDofSetIdIndices[setIt];
                        var slots = dofSet.GetValue<List<ReflLayoutData>>("Slots");
                        for (int slotIt = 0; slotIt < slots.Count; ++slotIt)
                        {
                            var slot = slots[slotIt];
                            ushort dofId = rigDofIds[idOffset + slotIt];
                            channelLookup[slot.GetValue<string>("Name")] = new DofChannel
                            {
                                Dof = dofId,
                                Layout = new BoneLayout
                                {
                                    Name = slot.GetValue<string>("Name"),
                                    Type = (BoneChannelType)slot.GetValue<uint>("Type")
                                }
                            };
                        }
                    }
                    break;
                }
                default: throw new NotImplementedException();
            }

            return channelLookup;
        }

        protected List<DofChannel> GetBoneLayoutsSorted(ReflLayoutData lytHierarchy)
        {
            var layouts = CreateDofLookupMap(lytHierarchy);
            return layouts
                .Values
                .OrderBy(x =>
                {
                    switch (x.Layout.Type)
                    {
                        case BoneChannelType.Quaternion: return 0;
                        case BoneChannelType.Translation: return 1;
                        case BoneChannelType.Scale:    return 2;
                        default: return 3;
                    }
                })
                .ToList();
        }
    }
    
    public class RawAnimationImporter : AnimationImporter
    {
        const int FLOATS_PER_VECTOR = 4; // ant and frostbite expect vectors to be aligned to 16 bytes
        const int FLOATS_PER_QUAT = 4;
        
        public RawAnimationImporter(AntAssetBank bank, ReflLayoutData rig, ReflLayoutData layoutHierarchy, FrostbiteVersion version)
            : base(bank, rig, layoutHierarchy, version)
        {
            
        }

        public void ImportAnimationV1(Animation animation, ReflLayoutData animData)
        {
            var boneLayoutsSorted = GetBoneLayoutsSorted(m_layoutHierarchyAsset);
            
            // setup our dof mapping
            ChannelToDof dofMappingContainer = new ChannelToDof();

            List<List<QuaternionKey>> quatChannels = new List<List<QuaternionKey>>();
            List<List<VectorKey>> vecChannels = new List<List<VectorKey>>();

            var animChannelLookup = animation.NodeAnimationChannels.ToDictionary(x => x.NodeName);
            foreach (var dofChannel in boneLayoutsSorted)
            {
                if (dofChannel.Layout.Name.Length < 3)
                    continue;
                    
                string boneName = dofChannel.Layout.Name.Substring(0, dofChannel.Layout.Name.Length - 2);
                switch (dofChannel.Layout.Type)
                {
                    case BoneChannelType.Quaternion:
                    {
                        if(animChannelLookup.TryGetValue(boneName, out var animChannel) && animChannel.HasRotationKeys)
                        {
                            dofMappingContainer.Channels.Add(dofChannel);
                            quatChannels.Add(animChannel.RotationKeys);
                        }
                        break;
                    }
                    case BoneChannelType.Translation:
                    {
                        if(animChannelLookup.TryGetValue(boneName, out var animChannel) && animChannel.HasPositionKeys)
                        {
                            dofMappingContainer.Channels.Add(dofChannel);
                            vecChannels.Add(animChannel.PositionKeys);   
                        }
                        break;
                    }
                    case BoneChannelType.Scale:
                    {
                        if(animChannelLookup.TryGetValue(boneName, out var animChannel) && animChannel.HasScalingKeys)
                        {
                            dofMappingContainer.Channels.Add(dofChannel);
                            vecChannels.Add(animChannel.ScalingKeys);   
                        }
                        break;
                    }
                }
            }
            
            var mappingAsset = dofMappingContainer.BuildMappingAsset(m_bank, m_fbVersion);
            var animBaseData = animData.GetValue<ReflLayoutData>("__base");
            animBaseData.SetValue("ChannelToDofAsset", mappingAsset.GetValue<Guid>("__guid"));

            // setup frame data
            int quatCount = quatChannels.Count;
            int vecCount = vecChannels.Count;
            int floatCount = 0;
            
            int frameSize = (quatCount * FLOATS_PER_QUAT) + (vecCount * FLOATS_PER_VECTOR) + floatCount;
            
            int quatOffset = 0;
            int vec3Offset = quatCount * FLOATS_PER_QUAT;
            int floatOffset = (quatCount * FLOATS_PER_QUAT) + (vecCount * FLOATS_PER_VECTOR);

            // setup key times
            List<double> keyTimes = new List<double>();
            foreach (var channel in animation.NodeAnimationChannels)
            {
                foreach (var key in channel.PositionKeys)
                    keyTimes.Add(key.Time);
                foreach(var key in channel.RotationKeys)
                    keyTimes.Add(key.Time);
                foreach(var key in channel.ScalingKeys)
                    keyTimes.Add(key.Time);
            }

            keyTimes = keyTimes.Distinct().OrderBy(t => t).ToList();
            
            // For each frame of a raw animation, data is laid out like this
            // [quats] [vectors] [floats]
            float[] frameData = new float[keyTimes.Count * frameSize];
            for (int frameIt = 0; frameIt < keyTimes.Count; frameIt++)
            {
                int frameBaseIdx = frameIt * frameSize;
                double keyTime = keyTimes[frameIt];
                
                for (int quatIdx = 0; quatIdx < quatCount; quatIdx++)
                {
                    Quaternion quat = GetQuatKey(keyTime, quatChannels[quatIdx]);
                    
                    int baseIdx = frameBaseIdx + quatOffset + quatIdx * FLOATS_PER_QUAT;

                    frameData[baseIdx + 0] = quat.X;
                    frameData[baseIdx + 1] = quat.Y;
                    frameData[baseIdx + 2] = quat.Z;
                    frameData[baseIdx + 3] = quat.W;
                }
                
                for (int vecIdx = 0; vecIdx < vecChannels.Count; vecIdx++)
                {
                    Vector3 vec = GetVectorKey(keyTime, vecChannels[vecIdx]);

                    int baseIdx = frameBaseIdx + vec3Offset + vecIdx * FLOATS_PER_VECTOR;

                    frameData[baseIdx + 0] = vec.X;
                    frameData[baseIdx + 1] = vec.Y;
                    frameData[baseIdx + 2] = vec.Z;
                    frameData[baseIdx + 3] = 0.0f;
                }
            }

            var keyTimesShort = new List<short>(keyTimes.Count);
            for (short i = 0; i < keyTimes.Count; i++)
                keyTimesShort.Add(i);

            animData.SetValue("KeyTimes", keyTimesShort);
            animData.SetValue("Data", frameData.ToList());
            animData.SetValue("FloatCount", (uint)floatCount);
            animData.SetValue("Vec3Count", (uint)vecCount);
            animData.SetValue("QuatCount", (uint)quatCount);
            animData.SetValue("NumKeys", (uint)keyTimesShort.Count);
            animData.SetValue("Cycle", true);
        }

        public void ImportAnimationV2(Animation animation, ReflLayoutData animData)
        {
            var boneLayoutsSorted = GetBoneLayoutsSorted(m_layoutHierarchyAsset);
            
            // setup our dof mapping
            ChannelToDof dofMappingContainer = new ChannelToDof();

            List<List<QuaternionKey>> quatChannels = new List<List<QuaternionKey>>();
            List<List<VectorKey>> vecChannels = new List<List<VectorKey>>();
            
            var animChannelLookup = animation.NodeAnimationChannels.ToDictionary(x => x.NodeName);
            foreach (var dofChannel in boneLayoutsSorted)
            {
                if (dofChannel.Layout.Name.Length < 3)
                    continue;
                    
                string boneName = dofChannel.Layout.Name.Substring(0, dofChannel.Layout.Name.Length - 2);
                switch (dofChannel.Layout.Type)
                {
                    case BoneChannelType.Quaternion:
                    {
                        if(animChannelLookup.TryGetValue(boneName, out var animChannel) && animChannel.HasRotationKeys)
                        {
                            dofMappingContainer.Channels.Add(dofChannel);
                            quatChannels.Add(animChannel.RotationKeys);
                        }
                        break;
                    }
                    case BoneChannelType.Translation:
                    {
                        if(animChannelLookup.TryGetValue(boneName, out var animChannel) && animChannel.HasPositionKeys)
                        {
                            dofMappingContainer.Channels.Add(dofChannel);
                            vecChannels.Add(animChannel.PositionKeys);   
                        }
                        break;
                    }
                    case BoneChannelType.Scale:
                    {
                        if(animChannelLookup.TryGetValue(boneName, out var animChannel) && animChannel.HasScalingKeys)
                        {
                            dofMappingContainer.Channels.Add(dofChannel);
                            vecChannels.Add(animChannel.ScalingKeys);   
                        }
                        break;
                    }
                }
            }
            
            var mappingAsset = dofMappingContainer.BuildMappingAsset(m_bank, m_fbVersion);
            var animBaseData = animData.GetValue<ReflLayoutData>("__base");
            animBaseData.SetValue("ChannelToDofAsset", mappingAsset.GetValue<Guid>("__guid"));

            // setup frame data
            int quatCount = quatChannels.Count;
            int vecCount = vecChannels.Count;
            int floatCount = 0;
            
            // setup local indices
            var mappingIndices = new ushort[quatCount + vecCount + floatCount];
            var channelIndices = new ushort[quatCount + vecCount + floatCount];

            uint channelIdx = 0;
            for (int quatIdx = 0; quatIdx < quatCount; quatIdx++)
            {
                mappingIndices[channelIdx] = (ushort)quatIdx;
                channelIndices[quatIdx] = (ushort)channelIdx;
                channelIdx++;
            }

            for (int vecIdx = 0; vecIdx < vecCount; vecIdx++)
            {
                mappingIndices[channelIdx] = (ushort)(quatCount + vecIdx);
                channelIndices[vecIdx + quatCount] = (ushort)channelIdx;
                channelIdx++;
            }
            
            int frameSize = (quatCount * FLOATS_PER_QUAT) + (vecCount * FLOATS_PER_VECTOR) + floatCount;
            
            int quatOffset = 0;
            int vec3Offset = quatCount * FLOATS_PER_QUAT;
            int floatOffset = (quatCount * FLOATS_PER_QUAT) + (vecCount * FLOATS_PER_VECTOR);

            // setup key times
            List<double> keyTimes = new List<double>();
            foreach (var channel in animation.NodeAnimationChannels)
            {
                foreach (var key in channel.PositionKeys)
                    keyTimes.Add(key.Time);
                foreach(var key in channel.RotationKeys)
                    keyTimes.Add(key.Time);
                foreach(var key in channel.ScalingKeys)
                    keyTimes.Add(key.Time);
            }

            keyTimes = keyTimes.Distinct().OrderBy(t => t).ToList();
            
            // For each frame of a raw animation, data is laid out like this
            // [quats] [vectors] [floats] [const quats] [const vectors] [const floats]
            float[] frameData = new float[keyTimes.Count * frameSize];
            for (int frameIt = 0; frameIt < keyTimes.Count; frameIt++)
            {
                int frameBaseIdx = frameIt * frameSize;
                double keyTime = keyTimes[frameIt];
                
                for (int quatIdx = 0; quatIdx < quatCount; quatIdx++)
                {
                    Quaternion quat = GetQuatKey(keyTime, quatChannels[quatIdx]);
                    
                    int baseIdx = frameBaseIdx + quatOffset + quatIdx * FLOATS_PER_QUAT;

                    frameData[baseIdx + 0] = quat.X;
                    frameData[baseIdx + 1] = quat.Y;
                    frameData[baseIdx + 2] = quat.Z;
                    frameData[baseIdx + 3] = quat.W;
                }
                
                for (int vecIdx = 0; vecIdx < vecChannels.Count; vecIdx++)
                {
                    Vector3 vec = GetVectorKey(keyTime, vecChannels[vecIdx]);

                    int baseIdx = frameBaseIdx + vec3Offset + vecIdx * FLOATS_PER_VECTOR;

                    frameData[baseIdx + 0] = vec.X;
                    frameData[baseIdx + 1] = vec.Y;
                    frameData[baseIdx + 2] = vec.Z;
                    frameData[baseIdx + 3] = 0.0f;
                }
            }

            var keyTimesShort = new List<short>(keyTimes.Count);
            for (short i = 0; i < keyTimes.Count; i++)
                keyTimesShort.Add(i);

            animData.SetValue("KeyTimes", keyTimesShort);
            animData.SetValue("MappingIndices", mappingIndices.ToList());
            animData.SetValue("ChannelIndices", channelIndices.ToList());
            animData.SetValue("Data", frameData.ToList());
            animData.SetValue("FloatCount", (ushort)floatCount);
            animData.SetValue("Vec3Count", (ushort)vecCount);
            animData.SetValue("QuatCount", (ushort)quatCount);
            animData.SetValue("NumKeys", (uint)keyTimesShort.Count);
            animData.SetValue("Cycle", false);
        }
        
        public override void ImportAnimation(Animation animation, ReflLayoutData animData)
        {
            switch (m_fbVersion)
            {
                case FrostbiteVersion.GW1: ImportAnimationV1(animation, animData); break;
                case FrostbiteVersion.GW2: ImportAnimationV2(animation, animData); break;
                default: throw new NotImplementedException();
            }
        }

        public static void TestRawAnimationImport(Animation anim, AntAssetBank bank, FrostbiteVersion version)
        {
            if (version == FrostbiteVersion.GW1)
            {
                if (!bank.Layouts.ContainsKey((ReflLayoutHash)2709637666))
                {
                    ReflLayout RawAnimationAsset = new ReflLayout("RawAnimationAsset", true, new[]
                    {
                        new ReflLayoutField(-3, "__guid",        ReflLayoutType.Guid),
                        new ReflLayoutField(-2, "__name",        ReflLayoutType.String),
                        new ReflLayoutField(-1, "__base",        ReflLayoutType.DataRef),
                        new ReflLayoutField(0,  "KeyTimes",      ReflLayoutType.Int16, ReflLayoutFlags.Array),
                        new ReflLayoutField(1,  "Data",          ReflLayoutType.Float, ReflLayoutFlags.Array),
                        new ReflLayoutField(2,  "FloatCount",    ReflLayoutType.UInt32),
                        new ReflLayoutField(3,  "Vec3Count",     ReflLayoutType.UInt32),
                        new ReflLayoutField(4,  "QuatCount",     ReflLayoutType.UInt32),
                        new ReflLayoutField(5,  "NumKeys",       ReflLayoutType.UInt32),
                        new ReflLayoutField(6,  "Cycle",         ReflLayoutType.Bool)
                    });
                    Debug.Assert(RawAnimationAsset.LayoutHash == (ReflLayoutHash)2709637666);
                    bank.Layouts.TryAddLayout(RawAnimationAsset);
                }
            }
            else
            {
                if (!bank.Layouts.ContainsKey((ReflLayoutHash)3860097082))
                {
                    ReflLayout RawAnimationAsset = new ReflLayout("RawAnimationAsset", true, new[]
                    {
                        new ReflLayoutField(-3, "__guid",           ReflLayoutType.Guid),
                        new ReflLayoutField(-2, "__name",           ReflLayoutType.String),
                        new ReflLayoutField(-1, "__base",           ReflLayoutType.DataRef),
                        new ReflLayoutField(0,  "KeyTimes",         ReflLayoutType.Int16, ReflLayoutFlags.Array),
                        new ReflLayoutField(1,  "MappingIndices",   ReflLayoutType.UInt16, ReflLayoutFlags.Array),
                        new ReflLayoutField(2,  "ChannelIndices",   ReflLayoutType.UInt16, ReflLayoutFlags.Array),
                        new ReflLayoutField(3,  "Data",             ReflLayoutType.Float, ReflLayoutFlags.Array),
                        new ReflLayoutField(4,  "ConstData",        ReflLayoutType.Float, ReflLayoutFlags.Array),
                        new ReflLayoutField(5,  "FloatCount",       ReflLayoutType.UInt16),
                        new ReflLayoutField(6,  "Vec3Count",        ReflLayoutType.UInt16),
                        new ReflLayoutField(7,  "QuatCount",        ReflLayoutType.UInt16),
                        new ReflLayoutField(8,  "ConstFloatCount",  ReflLayoutType.UInt16),
                        new ReflLayoutField(9,  "ConstVec3Count",   ReflLayoutType.UInt16),
                        new ReflLayoutField(10, "ConstQuatCount",   ReflLayoutType.UInt16),
                        new ReflLayoutField(11,  "NumKeys",         ReflLayoutType.UInt32),
                        new ReflLayoutField(12,  "Cycle",           ReflLayoutType.Bool)
                    });
                    Debug.Assert(RawAnimationAsset.LayoutHash == (ReflLayoutHash)3860097082);
                    bank.Layouts.TryAddLayout(RawAnimationAsset);
                }   
            }

            var tgtHierarchyGuid = version == FrostbiteVersion.GW1
                ? new Guid("0008511b-0000-0000-0000-000000000000")
                : new Guid("00082ebb-0000-0000-0000-000000000000");

            var tgtRigGuid = version == FrostbiteVersion.GW1
                ? new Guid("00081a14-0000-0000-0000-000000000000")
                : new Guid("00080608-0000-0000-0000-000000000000");

            var tgtRig = bank.GuidToAssetMap[tgtRigGuid];
            var tgtLytHierarchy = bank.GuidToAssetMap[tgtHierarchyGuid];
            
            var rawAnimData = bank.CreateAssetOfType("RawAnimationAsset", "MyNewAnimation Anim");
            ReflLayoutData animBaseData = bank.CreateDataOfType("AnimationAsset");
        
            animBaseData.SetValue("CodecType", 1380013856u); // RAW
            animBaseData.SetValue("AnimId", 1348865296u); // some kind of hash
            animBaseData.SetValue("TrimOffset", 0.0f);
            if(version == FrostbiteVersion.GW1)
                animBaseData.SetValue("Additive", false);
            rawAnimData.SetValue("__base", animBaseData);
            
            var newClipControllerData = bank.CreateAssetOfType("ClipControllerAsset", "MyNewAnimation");
            newClipControllerData.SetValue("__base", bank.CreateDataOfType("ControllerAsset"));
            newClipControllerData.SetValue("Target", tgtHierarchyGuid);
            newClipControllerData.SetValue("FPS", 30.0f);
            if (version == FrostbiteVersion.GW1)
            {
                newClipControllerData.SetValue("Anim", rawAnimData.GetValue<Guid>("__guid"));
                newClipControllerData.SetValue("FPSScale", 0.5f);
                newClipControllerData.SetValue<byte>("DeltaTrajectory", 3);   
            }
            else
            {
                newClipControllerData.SetValue<byte>("Modes", 3);
                newClipControllerData.GetValue<List<Guid>>("Anims").Add(rawAnimData.GetValue<Guid>("__guid"));
                newClipControllerData.SetValue("TimeScale", 0.5f);
                newClipControllerData.SetValue<byte>("TrajectoryAnimIndex", 0);
            }

            RawAnimationImporter importer = new RawAnimationImporter(bank, tgtRig, tgtLytHierarchy, version);
        
            importer.ImportAnimation(anim, rawAnimData);

            uint animTickCount = rawAnimData.GetValue<uint>("NumKeys") - 1;
            uint animTickCountDoubled = animTickCount * 2;
            
            animBaseData.SetValue("EndFrame", (ushort)animTickCount);
            newClipControllerData.SetValue("NumTicks", (float)animTickCountDoubled);

            Guid clipControllerGuid = newClipControllerData.GetValue<Guid>("__guid");
            if (version == FrostbiteVersion.GW1)
            {
                var cacIdleBlendSpace = bank.GuidToAssetMap[new Guid("00080cdc-0000-0000-0000-000000000000")];
                {
                    var diagram = cacIdleBlendSpace.GetValue<ReflLayoutData>("VoronoiDiagram");
                    var items = diagram.GetValue<List<ReflLayoutData>>("Items");
                    var firstItem = items[0];
                    firstItem.SetValue("BlendAsset", clipControllerGuid);
                }

                var cacIdleLSC = bank.GuidToAssetMap[new Guid("0008039C-0000-0000-0000-000000000000")];
                {
                    var baseData = cacIdleLSC.GetValue<ReflLayoutData>("__base");
                    baseData.SetValue("ControllerAsset", clipControllerGuid);
                }

                var cacIdleECE = bank.GuidToAssetMap[new Guid("00082371-0000-0000-0000-000000000000")];
                {
                    var baseData = cacIdleECE.GetValue<ReflLayoutData>("__base");
                    baseData.SetValue("ControllerAsset", clipControllerGuid);
                }   
            }
            else
            {
                var seqSb = bank.GuidToAssetMap[new Guid("0008bbf1-0000-0000-0000-000000000000")];
                {
                    seqSb.SetValue("Length", (float)animTickCountDoubled);
                    var tracks = seqSb.GetValue<List<ReflLayoutData>>("Tracks");
                    var trackOne = tracks[0];

                    var anims = trackOne.GetValue<List<ReflLayoutData>>("Anims");
                    var animOne = anims[0];
                    
                    animOne.SetValue("Asset", newClipControllerData.GetValue<Guid>("__guid"));
                    animOne.SetValue("EndInTime", (short)animTickCountDoubled);
                }

                var sbIdleMenuClipController = bank.GuidToAssetMap[new Guid("00084fdf-0000-0000-0000-000000000000")];
                {
                    sbIdleMenuClipController.SetValue("NumTicks", (float)animTickCountDoubled);
                    sbIdleMenuClipController.GetValue<List<Guid>>("Anims")[0] = rawAnimData.GetValue<Guid>("__guid");
                }
            }
        }
    }
}