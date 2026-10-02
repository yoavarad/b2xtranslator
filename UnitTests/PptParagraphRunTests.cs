using b2xtranslator.PptFileFormat;
using b2xtranslator.StructuredStorage.Reader;
using NUnit.Framework;
using System.IO;
using System.Linq;

namespace UnitTests
{
    // Issue #90: ParagraphRun mask layout vs [MS-PPT] PFMasks / TextPFException.
    // Master TextMasterStyleAtoms hold consecutive TextPFException + TextCFException pairs, so a wrong
    // field order or mask bit would desynchronise later levels and surface as reserved bits / bad sizes.
    [TestFixture]
    public class PptParagraphRunTests
    {
        // PFMasks reserved bits: 22 (reserved1), 23-25 (bulletBlip/bulletScheme/bulletHasScheme, MUST be 0 in
        // TextPFException), 26-31 (reserved2).
        private const uint ReservedMask = 0xFFC00000;

        [TestCase("WithLinks.ppt")]
        [TestCase("45537_Header.ppt")]
        public void MasterStyleParagraphRuns_FollowPfMasksLayout(string fixture)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "files", fixture);
            using (var reader = new StructuredStorageReader(path))
            {
                var ppt = new PowerpointDocument(reader);
                var runs = ppt.MainMasterRecords
                    .SelectMany(m => m.AllChildrenWithType<TextMasterStyleAtom>())
                    .SelectMany(a => a.PRuns)
                    .ToList();
                Assert.That(runs, Is.Not.Empty);
                Assert.That(runs.Any(r => r.Mask != ParagraphMask.None), "no run has any mask bit set");

                foreach (var r in runs)
                {
                    Assert.That((uint)r.Mask & ReservedMask, Is.EqualTo(0u), "reserved PFMasks bits set: " + r.Mask);

                    // bulletFlags exists iff any of A-D set.
                    Assert.That(r.BulletFlags.HasValue, Is.EqualTo(r.BulletFlagsFieldPresent));
                    // BulletFlags reserved (12 bits) MUST be zero.
                    if (r.BulletFlags.HasValue)
                        Assert.That(r.BulletFlags.Value & 0xFFF0, Is.EqualTo(0));

                    // BulletSize: 25..400 percent or -4000..-1 points.
                    if (r.BulletSize.HasValue)
                        Assert.That(r.BulletSize.Value, Is.InRange(-4000, 400).And.Not.InRange(0, 24));
                }
            }
        }
    }
}
