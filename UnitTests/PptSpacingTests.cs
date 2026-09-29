using b2xtranslator.PresentationMLMapping;
using NUnit.Framework;
using System.IO;
using System.Xml;

namespace UnitTests
{
    [TestFixture]
    public class PptSpacingTests
    {
        [TestCase(100, "spcPct", 100000)]  // 100% line height
        [TestCase(0, "spcPct", 0)]
        [TestCase(-8, "spcPts", 100)]      // 8 master units = 1 pt = 100 centipoints
        [TestCase(-576, "spcPts", 7200)]   // 1 inch = 72 pt
        [TestCase(-1, "spcPts", 13)]       // 12.5 rounds away from zero
        public void PptSpacingToOoxml_ConvertsPercentAndMasterUnits(int value, string expectedElement, int expectedVal)
        {
            int val = Utils.PptSpacingToOoxml(value, out string element);
            Assert.AreEqual(expectedElement, element);
            Assert.AreEqual(expectedVal, val);
        }

        [Test]
        public void WriteSpacing_EmitsDrawingMLElement()
        {
            var sw = new StringWriter();
            using (var w = XmlWriter.Create(sw, new XmlWriterSettings { OmitXmlDeclaration = true }))
            {
                Utils.WriteSpacing(w, -16);
            }
            StringAssert.Contains("<a:spcPts val=\"200\"", sw.ToString());
        }

        [TestCase(10800, 0)]       // centre
        [TestCase(0, -50000)]      // left/top edge
        [TestCase(21600, 50000)]   // right/bottom edge
        [TestCase(-4320, -70000)]  // outside the shape
        [TestCase(10801, 5)]       // fractional percent kept (was truncated to 0)
        [TestCase(10799, -5)]
        public void LegacyCalloutAdjustToOoxml_MapsOffsetFromCentre(int legacy, int expected)
        {
            Assert.AreEqual(expected, Utils.LegacyCalloutAdjustToOoxml(legacy));
        }
    }
}
