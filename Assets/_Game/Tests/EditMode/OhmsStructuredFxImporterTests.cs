using System.Collections.Generic;
using ArknightsACT.Editor.OHMS;
using NUnit.Framework;

namespace ArknightsACT.Tests
{
    public sealed class OhmsStructuredFxImporterTests
    {
        [Test]
        public void FilterRoots_UsesCaseInsensitiveIncludeAndExcludeTokens()
        {
            var scan = new OhmsStructuredFxImporter.ScanResult
            {
                RootGameObjects = new List<OhmsStructuredFxImporter.AssetRecord>
                {
                    new() { Name = "chen_skill_02_start" },
                    new() { Name = "chen_skill_02_start_nian#2" },
                    new() { Name = "other_effect" }
                }
            };

            var result = OhmsStructuredFxImporter.FilterRoots(
                scan,
                "CHEN_SKILL_02_",
                "_NIAN#2");

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Name, Is.EqualTo("chen_skill_02_start"));
        }
    }
}
