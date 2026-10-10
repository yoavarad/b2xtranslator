using NUnit.Framework;
using PptUtils = b2xtranslator.PresentationMLMapping.Utils;

namespace UnitTests
{
    [TestFixture]
    public class PptColorSchemeTests
    {
        // PPT scheme index (ColorSchemeAtom field order) -> DrawingML scheme color.
        // Must match ColorSchemeMapping.writeScheme + default clrMap
        // (bg1=lt1, tx1=dk1, bg2=lt2, tx2=dk2).
        [TestCase((byte)0x00, "bg1")]      // Background
        [TestCase((byte)0x01, "tx1")]      // TextAndLines
        [TestCase((byte)0x02, "bg2")]      // Shadows
        [TestCase((byte)0x03, "tx2")]      // TitleText
        [TestCase((byte)0x04, "accent1")]  // Fills
        [TestCase((byte)0x05, "accent2")]  // Accent
        [TestCase((byte)0x06, "hlink")]    // AccentAndHyperlink
        [TestCase((byte)0x07, "folHlink")] // AccentAndFollowedHyperlink
        public void GetSchemeColorName_MapsSchemeIndex(byte index, string expected)
        {
            Assert.That(PptUtils.getSchemeColorName(index), Is.EqualTo(expected));
        }

        [TestCase((byte)0x08)]
        [TestCase((byte)0xFE)]
        [TestCase((byte)0xFF)]
        public void GetSchemeColorName_OutOfRange_ReturnsEmpty(byte index)
        {
            Assert.That(PptUtils.getSchemeColorName(index), Is.Empty);
        }
    }
}
