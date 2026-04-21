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

    public enum StorageType
    {
        Invalid,
        Raw8,
        Raw16
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

        public override string ToString()
        {
            return $"{BoneName}: {LayoutIndex}";
        }
    }

    public class ChannelToDof
    {
        public StorageType StorageType { get; private set; } = StorageType.Invalid;
        public List<DofChannel> Channels { get; private set; } = new List<DofChannel>();

        public List<byte> BuildDofIndices()
        {
            bool needsShortIndices = Channels.Count(x => x.LayoutIndex > byte.MaxValue) > 0;
            
            int numDofIndices = needsShortIndices
                ? Channels.Count * 2
                : Channels.Count;

            StorageType = needsShortIndices ? StorageType.Raw16 : StorageType.Raw8;
            
            List<byte> dofIndices = new List<byte>(numDofIndices);

            if (needsShortIndices)
            {
                foreach (var channel in Channels)
                {
                    dofIndices.Add((byte)(channel.LayoutIndex >> 8));
                    dofIndices.Add((byte)(channel.LayoutIndex & 0xFF));
                }
            }
            else
            {
                foreach (var channel in Channels)
                {
                    Debug.Assert(channel.LayoutIndex <= byte.MaxValue);
                    dofIndices.Add((byte)channel.LayoutIndex);
                }
            }

            return dofIndices;
        }
    }
    
    public static class RawAnimationImporter
    {
        public static Quaternion GetQuatKey(double timeInTicks, List<QuaternionKey> keys)
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
        
        public static Vector3 GetVectorKey(double timeInTicks, List<VectorKey> keys)
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
        
        public static void ParseLayoutHierarchy(ReflLayoutData lytHierarchy, AntAssetBank bank, List<BoneLayout> channelNames)
        {
            foreach (var lytGuid in lytHierarchy.GetValue<List<Guid>>("LayoutAssets"))
            {
                var layoutAsset = bank.GuidToAssetMap[lytGuid];

                if (layoutAsset.Layout.Name == "LayoutAsset")
                {
                    foreach (var layoutEntry in layoutAsset.GetValue<List<ReflLayoutData>>("Slots"))
                    {
                        string name = layoutEntry.GetValue<string>("Name");
                        uint type = layoutEntry.GetValue<uint>("Type");

                        channelNames.Add(new BoneLayout{Name = name, Type = (BoneChannelType)type});
                    }
                }
                else
                {
                    Debug.Assert(layoutAsset.Layout.Name == "DeltaTrajLayoutAsset");
                    for (int i = 0; i < 8; i++)
                    {
                        channelNames.Add(new BoneLayout { Name = i.ToString(), Type = BoneChannelType.Quaternion });
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
                        case BoneChannelType.Quaternion: return 0;
                        case BoneChannelType.Translation: return 1;
                        case BoneChannelType.Scale:    return 2;
                        default: return 3;
                    }
                })
                .ToList();

            List<List<QuaternionKey>> quatChannels = new List<List<QuaternionKey>>();
            List<List<VectorKey>> vecChannels = new List<List<VectorKey>>();

            {
                var animChannelLookup = animation.NodeAnimationChannels.ToDictionary(x => x.NodeName);
                foreach (var layout in boneLayoutsSorted)
                {
                    if (layout.Name.Length < 3)
                        continue;
                    
                    string boneName = layout.Name.Substring(0, layout.Name.Length - 2);
                    switch (layout.Type)
                    {
                        case BoneChannelType.Quaternion:
                        {
                            if(animChannelLookup.TryGetValue(boneName, out var channel) && channel.HasRotationKeys)
                            {
                                dofMappingContainer.Channels.Add(new DofChannel{BoneName = layout.Name, LayoutIndex = (ushort)boneLayouts.IndexOf(layout)});
                                quatChannels.Add(channel.RotationKeys);   
                            }
                            break;
                        }
                        case BoneChannelType.Translation:
                        {
                            if(animChannelLookup.TryGetValue(boneName, out var channel) && channel.HasPositionKeys)
                            {
                                dofMappingContainer.Channels.Add(new DofChannel{BoneName = layout.Name, LayoutIndex = (ushort)boneLayouts.IndexOf(layout)});
                                vecChannels.Add(channel.PositionKeys);   
                            }
                            break;
                        }
                        case BoneChannelType.Scale:
                        {
                            if(animChannelLookup.TryGetValue(boneName, out var channel) && channel.HasScalingKeys)
                            {
                                dofMappingContainer.Channels.Add(new DofChannel{BoneName = layout.Name, LayoutIndex = (ushort)boneLayouts.IndexOf(layout)});
                                vecChannels.Add(channel.ScalingKeys);   
                            }
                            break;
                        }
                    }
                }
            }

            var dofIndices = dofMappingContainer.BuildDofIndices();
            channelToDofData.SetValue("StorageType", (uint)dofMappingContainer.StorageType);
            channelToDofData.SetValue("IndexData", dofIndices);

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
            
            rawAnimData.SetValue("KeyTimes", keyTimesShort);
            rawAnimData.SetValue("Data", frameData.ToList());
            rawAnimData.SetValue("FloatCount", (uint)floatCount);
            rawAnimData.SetValue("Vec3Count", (uint)vecCount);
            rawAnimData.SetValue("QuatCount", (uint)quatCount);
            rawAnimData.SetValue("NumKeys", (uint)keyTimesShort.Count);
            rawAnimData.SetValue("Cycle", true);
        }

        public static void TestRawAnimationImport(Animation anim, AntAssetBank bank)
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

            uint keyStartId = (uint)(bank.AssetData.Count + 2);
            
            ReflLayoutData rawAnimData = ReflLayoutData.CreateNew(bank.Layouts[(ReflLayoutHash)2709637666]);
            ReflLayoutData animBaseData = ReflLayoutData.CreateNew(bank.Layouts[(ReflLayoutHash)1220958457]);
            ReflLayoutData newChannelToDofData = ReflLayoutData.CreateNew(bank.Layouts[(ReflLayoutHash)1692914995]);
            ReflLayoutData newClipControllerData = ReflLayoutData.CreateNew(bank.Layouts[(ReflLayoutHash)3080919431]);
            
            AssetKey keyDof = new AssetKey(keyStartId++, true);
            newChannelToDofData.SetValue("__guid", keyDof.ToGuid());
            newChannelToDofData.SetValue("__name", "ChannelToDofSetVirtualAsset");
        
            animBaseData.SetValue("CodecType", 1380013856u); // RAW
            animBaseData.SetValue("AnimId", 1348865296u); // some kind of hash
            animBaseData.SetValue("TrimOffset", 0.0f);
            animBaseData.SetValue<ushort>("EndFrame", 60);
            animBaseData.SetValue("Additive", false);
            animBaseData.SetValue("ChannelToDofAsset", keyDof.ToGuid());

            AssetKey keyAnim = new AssetKey(keyStartId++, true);
            rawAnimData.SetValue("__guid", keyAnim.ToGuid());
            rawAnimData.SetValue("__name", "Cactus_New_Idle2 Anim");
            rawAnimData.SetValue("__base", animBaseData);
            
            AssetKey keyClipController = new AssetKey(keyStartId++, true);
            newClipControllerData.SetValue("__guid", keyClipController.ToGuid());
            newClipControllerData.SetValue("__name", "Cactus_New_Idle2");
            newClipControllerData.SetValue("__base", ReflLayoutData.CreateNew(bank.Layouts[(ReflLayoutHash)3345881505]));
            newClipControllerData.SetValue("Target", new Guid("0008511b-0000-0000-0000-000000000000"));
            newClipControllerData.SetValue("Anim", keyAnim.ToGuid());
            newClipControllerData.SetValue("NumTicks", 60.0f);
            newClipControllerData.SetValue("FPS", 20.0f);
            newClipControllerData.SetValue("FPSScale", 1.0f);
            newClipControllerData.SetValue<byte>("DeltaTrajectory", 3);

            bank.AssetData.Add(newClipControllerData);
            bank.AssetData.Add(newChannelToDofData);
            bank.AssetData.Add(rawAnimData);
            
            var skelAsset = bank.GuidToAssetMap[new Guid("00081b32-0000-0000-0000-000000000000")];
            var rigAsset = bank.GuidToAssetMap[new Guid("00081a14-0000-0000-0000-000000000000")];

            var channelNames = new List<BoneLayout>();
            
            {
                var lytHierarchy = bank.GuidToAssetMap[new Guid("0008511b-0000-0000-0000-000000000000")];
            
                ParseLayoutHierarchy(lytHierarchy, bank, channelNames);
            }
        
            ImportAnimation(anim, channelNames, rawAnimData, newChannelToDofData);
            
            animBaseData.SetValue("EndFrame", (ushort)rawAnimData.GetValue<uint>("NumKeys"));
            newClipControllerData.SetValue("NumTicks", (float)rawAnimData.GetValue<uint>("NumKeys"));

            var cacIdleBlendSpace = bank.GuidToAssetMap[new Guid("00080cdc-0000-0000-0000-000000000000")];
            {
                var diagram = cacIdleBlendSpace.GetValue<ReflLayoutData>("VoronoiDiagram");
                var items = diagram.GetValue<List<ReflLayoutData>>("Items");
                var firstItem = items[0];
                firstItem.SetValue("BlendAsset", keyClipController.ToGuid());
            }

            var cacIdleLSC = bank.GuidToAssetMap[new Guid("0008039C-0000-0000-0000-000000000000")];
            {
                var baseData = cacIdleLSC.GetValue<ReflLayoutData>("__base");
                baseData.SetValue("ControllerAsset", keyClipController.ToGuid());
            }

            var cacIdleECE = bank.GuidToAssetMap[new Guid("00082371-0000-0000-0000-000000000000")];
            {
                var baseData = cacIdleECE.GetValue<ReflLayoutData>("__base");
                baseData.SetValue("ControllerAsset", keyClipController.ToGuid());
            }
        }
    }
}