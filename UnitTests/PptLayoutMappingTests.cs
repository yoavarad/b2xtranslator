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

        [TestCase(SlideLayoutType.TwoRowsBottomTwoColumns)]
        [TestCase(SlideLayoutType.TitleMaster)]
        [TestCase(SlideLayoutType.MasterNotes)]
        [TestCase(SlideLayoutType.NotesTitleAndBody)]
        [TestCase(SlideLayoutType.Handout)]
        public void SlideLayoutTypeToFilename_UnmappedTypes_DoNotThrow(SlideLayoutType type)
        {
            var placeholders = new[] { PlaceholderEnum.Title, PlaceholderEnum.Body, PlaceholderEnum.Body };
            string name = null;
            Assert.DoesNotThrow(() => name = PptUtils.SlideLayoutTypeToFilename(type, placeholders));
            Assert.IsNotNull(PptUtils.GetDefaultDocument("slideLayouts." + name).DocumentElement);
        }

        [TestCase(SlideLayoutType.TwoColumnsLeftTwoRows)]
        [TestCase(SlideLayoutType.TwoColumnsRightTwoRows)]
        [TestCase(SlideLayoutType.TwoRowsAndTitle)]
        public void SlideLayoutTypeToFilename_UnmappedPlaceholderCombos_DoNotThrow(SlideLayoutType type)
        {
            var placeholders = new[] { PlaceholderEnum.Title, PlaceholderEnum.Table, PlaceholderEnum.Table };
            string name = null;
            Assert.DoesNotThrow(() => name = PptUtils.SlideLayoutTypeToFilename(type, placeholders));
            Assert.IsNotNull(PptUtils.GetDefaultDocument("slideLayouts." + name).DocumentElement);
        }
    }
}
