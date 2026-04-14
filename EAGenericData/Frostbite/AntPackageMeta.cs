namespace EAGenericData.Frostbite
{
    public class AntPackageMeta
    {
        public uint MetaBytes { get; set; }
        public AntPackagingType PackageType { get; set; }
        public uint AssetCount { get; set; }
        public uint ImportCount { get; set; }
        public uint AssetBytes { get; set; }
        public uint ExtraBytes { get; set; }
    }

    public class StaticAntPackageMeta : AntPackageMeta
    {
        public uint TotalNonStaticAssetCount { get; set; }
        public uint MaxNonStaticAssetCount { get; set; }
        public uint MaxNonStaticImportCount { get; set; }
        public uint StaticBundleImportBucketSize { get; set; }
        public uint StaticBundleImportBucketEntries { get; set; }
        public uint StaticBundleFlatImportCount { get; set; }
        public uint StaticBundleImportCountInSlots { get; set; }
        public uint StaticGuidToIntSize { get; set; }
    }
}