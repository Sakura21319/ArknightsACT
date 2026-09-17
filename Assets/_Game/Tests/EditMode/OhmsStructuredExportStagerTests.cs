using System;
using System.IO;
using System.Text;
using ArknightsACT.Editor.OHMS;
using NUnit.Framework;

namespace ArknightsACT.Tests
{
    public sealed class OhmsStructuredExportStagerTests
    {
        [Test]
        public void Prepare_NormalizesResolvablePointersWithoutChangingSourceOrBinaryPayloads()
        {
            var sourceRoot = Path.Combine(
                Path.GetTempPath(),
                "ArknightsACT-OhmsStager-" + Guid.NewGuid().ToString("N"));
            var thingsRoot = Path.Combine(sourceRoot, "things");
            Directory.CreateDirectory(thingsRoot);

            const string index = "{\"Assets\":[{\"ID\":0,\"PathID\":101}]}";
            const string jsonPayload =
                "{\"resolved\":{\"m_FileID\":2,\"m_PathID\":101}," +
                "\"missing\":{\"m_FileID\":3,\"m_PathID\":999}}";
            var binaryPayload = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x00, 0xff };

            File.WriteAllText(Path.Combine(sourceRoot, "assets.json"), index, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(thingsRoot, "0.ttbin"), jsonPayload, new UTF8Encoding(false));
            File.WriteAllBytes(Path.Combine(thingsRoot, "1.ttbin"), binaryPayload);

            try
            {
                var result = OhmsStructuredExportStager.Prepare(sourceRoot);
                var stagedJson = File.ReadAllText(Path.Combine(result.StagingRoot, "things", "0.ttbin"));

                Assert.That(result.KnownPathIds, Is.EqualTo(1));
                Assert.That(result.JsonPayloads, Is.EqualTo(1));
                Assert.That(result.RewrittenPointers, Is.EqualTo(1));
                StringAssert.Contains("\"m_FileID\":0,\"m_PathID\":101", stagedJson);
                StringAssert.Contains("\"m_FileID\":3,\"m_PathID\":999", stagedJson);
                Assert.That(File.ReadAllBytes(Path.Combine(result.StagingRoot, "things", "1.ttbin")),
                    Is.EqualTo(binaryPayload));
                Assert.That(File.ReadAllText(Path.Combine(thingsRoot, "0.ttbin")), Is.EqualTo(jsonPayload));
            }
            finally
            {
                if (Directory.Exists(sourceRoot))
                    Directory.Delete(sourceRoot, true);
            }
        }
    }
}
