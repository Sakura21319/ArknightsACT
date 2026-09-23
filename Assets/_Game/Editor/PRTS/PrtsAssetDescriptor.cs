#if UNITY_EDITOR
using System;
namespace ArknightsACT.Editor.PRTS
{
    internal sealed class PrtsAssetDescriptor
    {
        public string DisplayName { get; }
        public string SourcePage { get; }
        public string RemoteDirectory { get; }
        public string BaseName { get; }
        public string PrefabKey { get; }
        public string TargetDirectory { get; }
        public string Role { get; }
        public float TargetWorldHeight { get; }
        public float FeetLocalY { get; }
        public float SafeInitialScale { get; }

        public PrtsAssetDescriptor(
            string displayName,
            string sourcePage,
            string remoteDirectory,
            string baseName,
            string targetDirectory,
            string role,
            float targetWorldHeight,
            float feetLocalY,
            float safeInitialScale,
            string prefabKey = null)
        {
            DisplayName = displayName;
            SourcePage = sourcePage;
            RemoteDirectory = remoteDirectory;
            BaseName = baseName;
            PrefabKey = string.IsNullOrWhiteSpace(prefabKey) ? baseName : prefabKey;
            TargetDirectory = targetDirectory;
            Role = role;
            TargetWorldHeight = targetWorldHeight;
            FeetLocalY = feetLocalY;
            SafeInitialScale = safeInitialScale;
        }
    }
}
#endif
