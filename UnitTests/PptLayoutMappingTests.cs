using b2xtranslator.PptFileFormat;
using NUnit.Framework;
using PptUtils = b2xtranslator.PresentationMLMapping.Utils;

namespace UnitTests
{
    [TestFixture]
    public class PptLayoutMappingTests
    {
        // MS-PPT PlaceholderEnum PT_VerticalTitle / PT_VerticalBody -> ST_PlaceholderType.
        [TestCase(PlaceholderEnum.VerticalTextTitle, "title")]
        [TestCase(PlaceholderEnum.VerticalTextBody, "body")]
        public void PlaceholderIdToXMLValue_MapsVerticalPlaceholders(PlaceholderEnum pid, string expected)
        {
            Assert.That(PptUtils.PlaceholderIdToXMLValue(pid), Is.EqualTo(expected));
        }

        // MS-PPT SL_VerticalTitleBody -> ST_SlideLayoutType "vertTitleAndTx".
        [Test]
        public void SlideLayoutTypeToFilename_MapsVerticalTitleBody()
        {
            var placeholders = new[] { PlaceholderEnum.VerticalTextTitle, PlaceholderEnum.VerticalTextBody };
            string name = PptUtils.SlideLayoutTypeToFilename(SlideLayoutType.VerticalTitleRightBodyLeft, placeholders);

            Assert.That(name, Is.EqualTo("vertTitleAndTx"));
            Assert.IsNotNull(PptUtils.GetDefaultDocument("slideLayouts." + name).DocumentElement);
        }

        // MS-PPT SL_VerticalTitleBodyTwoRows -> ST_SlideLayoutType "vertTitleAndTxOverChart".
        [Test]
        public void SlideLayoutTypeToFilename_MapsVerticalTitleBodyTwoRows()
        {
            var placeholders = new[] { PlaceholderEnum.VerticalTextTitle, PlaceholderEnum.VerticalTextBody, PlaceholderEnum.Graph };
            string name = PptUtils.SlideLayoutTypeToFilename(SlideLayoutType.VerticalTitleRightBodyLeftTwoRows, placeholders);

            Assert.That(name, Is.EqualTo("vertTitleAndTxOverChart"));
            Assert.IsNotNull(PptUtils.GetDefaultDocument("slideLayouts." + name).DocumentElement);
        }
    }
}
