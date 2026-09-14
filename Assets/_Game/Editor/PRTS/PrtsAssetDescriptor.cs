#if UNITY_EDITOR
namespace ArknightsACT.Editor.PRTS
{
    internal sealed class PrtsAssetDescriptor
    {
        public string DisplayName { get; }
        public string SourcePage { get; }
        public string RemoteDirectory { get; }
        public string BaseName { get; }
        public string TargetDirectory { get; }
        public string Role { get; }

        public PrtsAssetDescriptor(
            string displayName,
            string sourcePage,
            string remoteDirectory,
            string baseName,
            string targetDirectory,
            string role)
        {
            DisplayName = displayName;
            SourcePage = sourcePage;
            RemoteDirectory = remoteDirectory;
            BaseName = baseName;
            TargetDirectory = targetDirectory;
            Role = role;
        }
    }
}
#endif
