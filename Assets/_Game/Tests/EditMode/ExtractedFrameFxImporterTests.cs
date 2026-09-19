using System;
using System.IO;
using ArknightsACT.Editor.Effects;
using NUnit.Framework;

namespace ArknightsACT.Tests
{
    public sealed class ExtractedFrameFxImporterTests
    {
        [Test]
        public void ResolveFramesRootAcceptsExtractorEffectsDirectory()
        {
            var root = Path.Combine(Path.GetTempPath(), "ArknightsACT-FxImporter-" + Guid.NewGuid().ToString("N"));
            var frames = Path.Combine(root, "effects", "frames");
            Directory.CreateDirectory(Path.Combine(frames, "skill_02_start"));
            try
            {
                Assert.That(
                    ExtractedFrameFxImporter.ResolveFramesRoot(root),
                    Is.EqualTo(frames));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void ResolveFramesRootAcceptsDirectFramesDirectory()
        {
            var root = Path.Combine(Path.GetTempPath(), "ArknightsACT-FxImporter-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "skill_03_hit_01"));
            try
            {
                Assert.That(
                    ExtractedFrameFxImporter.ResolveFramesRoot(root),
                    Is.EqualTo(root));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }
    }
}
