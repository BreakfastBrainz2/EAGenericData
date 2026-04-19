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
        Rotation = 14,
        Position = 2049856663,
        Scale = 2049856454,
    }

    public enum StorageType
    {
        Read = 0x0,
        Overwrite = 0x1,
        Append = 0x2,
        Invalid = 0x3,
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
        public string BoneName;
        public ushort LayoutIndex;
    }

    public class ChannelToDof
    {
        public StorageType StorageType { get; set; } = StorageType.Overwrite;
        public List<DofChannel> Channels { get; private set; } = new List<DofChannel>();
    }
    
    public static class RawAnimationImporter
    {
        public static Quaternion GetQuatKey(double timeInTicks, NodeAnimationChannel channel)
        {
            var keys = channel.RotationKeys;

            if (keys.Count == 0)
                return Quaternion.Identity;

            //if (keys.Count == 1)
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
        
        public static Vector3 GetTransKey(double timeInTicks, NodeAnimationChannel channel)
        {
            var keys = channel.PositionKeys;

            if (keys.Count == 0)
                return Vector3.Zero;

            //if (keys.Count == 1)
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
        
        public static Vector3 GetScaleKey(double timeInTicks, NodeAnimationChannel channel)
        {
            var keys = channel.ScalingKeys;

            if (keys.Count == 0)
                return Vector3.Zero;

            //if (keys.Count == 1)
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
        
        public static void ParseLayoutHierarchy(ReflLayoutData lytHierarchy, AntAssetBank bank, List<BoneLayout> channelNames)
        {
            foreach (var lytGuid in lytHierarchy.GetValue<List<Guid>>(0)) // LayoutAssets
            {
                var layoutAsset = bank.GuidToAssetMap[lytGuid];

                if (layoutAsset.Layout.Name == "LayoutAsset")
                {
                    foreach (var layoutEntry in layoutAsset.GetValue<List<ReflLayoutData>>(2)) // Slots
                    {
                        string name = layoutEntry.GetValue<string>(0);
                        uint type = layoutEntry.GetValue<uint>(1);

                        channelNames.Add(new BoneLayout{Name = name, Type = (BoneChannelType)type});
                    }
                }
                else
                {
                    for (int i = 0; i < 8; i++)
                    {
                        channelNames.Add(new BoneLayout { Name = i.ToString(), Type = BoneChannelType.Rotation });
                    }
                }
            }
        }
        
        public static void ImportAnimation(Animation animation, List<BoneLayout> boneLayouts, ReflLayoutData rawAnimData, ReflLayoutData channelToDofData)
        {
            const int FLOATS_PER_VECTOR = 4; // ant and frostbite expect vectors to be aligned to 16 bytes
            const int FLOATS_PER_QUAT = 4;

            // setup our dof mapping
            ChannelToDof dofMappingContainer = new ChannelToDof();
            
            List<BoneLayout> boneLayoutsSorted = boneLayouts
                .OrderBy(x =>
                {
                    switch (x.Type)
                    {
                        case BoneChannelType.Rotation: return 0;
                        case BoneChannelType.Position: return 1;
                        case BoneChannelType.Scale:    return 2;
                        default: return 3;
                    }
                })
                .ToList();

            List<NodeAnimationChannel> quatChannels = new List<NodeAnimationChannel>();
            List<NodeAnimationChannel> transChannels = new List<NodeAnimationChannel>();
            for (int i = 0; i < boneLayoutsSorted.Count; i++)
            {
                BoneLayout layout = boneLayoutsSorted[i];
                if (layout.Name.Length < 3)
                    continue;
                
                string boneName = layout.Name.Substring(0, layout.Name.Length - 2);
                switch (layout.Type)
                {
                    case BoneChannelType.Rotation:
                    {
                        var quatChannel = animation.NodeAnimationChannels.Find(x => x.HasRotationKeys && x.NodeName == boneName);
                        if (quatChannel != null)
                        {
                            dofMappingContainer.Channels.Add(new DofChannel{BoneName = layout.Name, LayoutIndex = (ushort)boneLayouts.IndexOf(layout)});
                            quatChannels.Add(quatChannel);   
                        }
                        break;
                    }
                    case BoneChannelType.Position:
                    {
                        var transChannel = animation.NodeAnimationChannels.Find(x => x.HasPositionKeys && x.NodeName == boneName);
                        if (transChannel != null)
                        {
                            dofMappingContainer.Channels.Add(new DofChannel{BoneName = layout.Name, LayoutIndex = (ushort)boneLayouts.IndexOf(layout)});
                            transChannels.Add(transChannel);   
                        }
                        break;
                    }
                }
            }
            
            channelToDofData.SetValue(0, (uint)dofMappingContainer.StorageType);
            
            List<byte> dofIndices = new List<byte>(dofMappingContainer.Channels.Count * 2);

            if (dofMappingContainer.Channels.Count > 256)
            {
                foreach (var channel in dofMappingContainer.Channels)
                {
                    dofIndices.Add((byte)(channel.LayoutIndex >> 8));
                    dofIndices.Add((byte)(channel.LayoutIndex & 0xFF));
                }   
            }
            else
            {
                foreach (var channel in dofMappingContainer.Channels)
                {
                    Debug.Assert(channel.LayoutIndex <= byte.MaxValue);
                    dofIndices.Add((byte)channel.LayoutIndex);
                }
            }
            
            channelToDofData.SetValue(1, dofIndices);

            // setup frame data
            int quatCount = quatChannels.Count;
            int vecCount = transChannels.Count;
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
            
            float[] frameData = new float[keyTimes.Count * frameSize];
            for (int i = 0; i < keyTimes.Count; i++)
            {
                int frameBase = i * frameSize;
                double keyTime = keyTimes[i];
                
                for (int quatIdx = 0; quatIdx < quatCount; quatIdx++)
                {
                    var quatChannel = quatChannels[quatIdx];
                    Quaternion quat = GetQuatKey(keyTime, quatChannel);
                    
                    int baseIdx = frameBase + quatOffset + quatIdx * FLOATS_PER_QUAT;

                    frameData[baseIdx + 0] = quat.X;
                    frameData[baseIdx + 1] = quat.Y;
                    frameData[baseIdx + 2] = quat.Z;
                    frameData[baseIdx + 3] = quat.W;
                }

                for (int transIdx = 0; transIdx < vecCount; transIdx++)
                {
                    var transChannel = transChannels[transIdx];
                    Vector3 trans = GetTransKey(keyTime, transChannel);
                    
                    int baseIdx = frameBase + vec3Offset + transIdx * FLOATS_PER_VECTOR;

                    frameData[baseIdx + 0] = trans.X;
                    frameData[baseIdx + 1] = trans.Y;
                    frameData[baseIdx + 2] = trans.Z;
                    frameData[baseIdx + 3] = 0.0f;
                }
            }

            List<short> keyTimesShort = new List<short>(keyTimes.Count);
            for (short i = 0; i < keyTimes.Count; i++)
            {
                keyTimesShort.Add(i);
            }
            //List<short> keyTimesShort = keyTimes.Select(x => (short)x).ToList();
            
            rawAnimData.SetValue(0, keyTimesShort);
            rawAnimData.SetValue(1, frameData.ToList());
            rawAnimData.SetValue(2, (uint)floatCount);
            rawAnimData.SetValue(3, (uint)vecCount);
            rawAnimData.SetValue(4, (uint)quatCount);
            rawAnimData.SetValue(5, (uint)keyTimesShort.Count);
            rawAnimData.SetValue(6, true);
        }

        public static void TestRawAnimationImport(Animation anim, AntAssetBank bank)
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
            
            ReflLayoutData rawDat = ReflLayoutData.CreateNew(RawAnimationAsset);
            ReflLayoutData baseDat = ReflLayoutData.CreateNew(bank.Layouts[(ReflLayoutHash)1220958457]);
            ReflLayoutData newChannelToDofDat = ReflLayoutData.CreateNew(bank.Layouts[(ReflLayoutHash)1692914995]);

            AssetKey keyDof = new AssetKey((uint)(bank.AssetData.Count + 2), true);
            newChannelToDofDat.SetValue(-3, keyDof.ToGuid());
            newChannelToDofDat.SetValue(-2, "ChannelToDofSetVirtualAsset");
        
            baseDat.SetValue(0, 1380013856u); // codec type
            baseDat.SetValue(1, 1348865296u); // anim id
            baseDat.SetValue(2, 0.0f);
            baseDat.SetValue<ushort>(3, 240);
            baseDat.SetValue(4, false);
            baseDat.SetValue(5, keyDof.ToGuid());

            AssetKey keyAnim = new AssetKey((uint)(bank.AssetData.Count + 3), true);
            rawDat.SetValue(-3, keyAnim.ToGuid());
            rawDat.SetValue(-2, "Cactus_New_Idle2 Anim");
            rawDat.SetValue(-1, baseDat);

            bank.AssetData.Add(newChannelToDofDat);
            bank.AssetData.Add(rawDat);
            
            var skelAsset = bank.GuidToAssetMap[new Guid("00081b32-0000-0000-0000-000000000000")];
            var rigAsset = bank.GuidToAssetMap[new Guid("00081a14-0000-0000-0000-000000000000")];
            var chToDof_idle = bank.GuidToAssetMap[new Guid("00084476-0000-0000-0000-000000000000")];

            var channelNames = new List<BoneLayout>();//new Dictionary<string, BoneChannelType>();
            
            //foreach (var lytHierarchyGuid in rigAsset.GetValue<List<Guid>>(2)) // dof sets
            {
                //var lytHierarchy = bank.GuidToAssetMap[lytHierarchyGuid];
                var lytHierarchy = bank.GuidToAssetMap[new Guid("0008511b-0000-0000-0000-000000000000")];
            
                ParseLayoutHierarchy(lytHierarchy, bank, channelNames);
            }

            var dofIndexData = chToDof_idle.GetValue<List<byte>>(1);

            var actualDofIndexData = Array.Empty<uint>();

            if (dofIndexData.Count > 256)
            {
                ushort[] usDofIds = new ushort[dofIndexData.Count / 2];
                for (int i = 0; i < dofIndexData.Count / 2; i++)
                {
                    usDofIds[i] = (ushort)((dofIndexData[i * 2] << 8) | dofIndexData[i * 2 + 1]);
                }

                actualDofIndexData = Array.ConvertAll(usDofIds, val => checked((uint)val));
            }
        
            var dofChannelsMap = new Dictionary<string, uint>(actualDofIndexData.Length);
            for (int i = 0; i < actualDofIndexData.Length; i++)
            {
                int chId = (int)actualDofIndexData[i];
                if (chId >= 0 && chId < channelNames.Count)
                {
                    dofChannelsMap.Add(channelNames[chId].Name, actualDofIndexData[i]);
                }
            }
        
            ImportAnimation(anim, channelNames, rawDat, newChannelToDofDat);
            
            var cacIdleClipController = bank.GuidToAssetMap[new Guid("00080ef1-0000-0000-0000-000000000000")];
            {
                //cacIdleClipController.SetValue(0, new Guid("000852ca-0000-0000-0000-000000000000"));
                cacIdleClipController.SetValue(1, rawDat.GetValue<Guid>(-3)); // Anim
                bank.ImportNodes[672] = new AntImportNode { Key = uint.MaxValue, Tid = ulong.MaxValue };
                //cacIdleClipController.SetValue(3, 5.0f); // NumTicks
            }
        }
    }
}