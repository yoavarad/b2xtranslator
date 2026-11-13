using System.IO;
using System.Xml;
using b2xtranslator.OfficeDrawing;
using b2xtranslator.PptFileFormat;
using b2xtranslator.PresentationMLMapping;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class FillMappingTests
    {
        [Test]
        public void Apply_ShadeColorsWithoutShadeType_DoesNotThrow()
        {
            // 1 element, cbElem 4: nElems, nElemsAlloc, cbElem, then one COLORREF (red, no scheme flags)
            byte[] colors = { 1, 0, 1, 0, 4, 0, 0xFF, 0, 0, 0 };
            var slide = new Slide(new BinaryReader(new MemoryStream()), 0, 1006, 0, 0);
            var so = new ShapeOptions();
            slide.Children.Add(so);
            so.ParentRecord = slide;
            so.OptionsByID[ShapeOptions.PropertyId.fillType] =
                new ShapeOptions.OptionEntry { pid = ShapeOptions.PropertyId.fillType, op = 4 };
            so.OptionsByID[ShapeOptions.PropertyId.fillShadeColors] =
                new ShapeOptions.OptionEntry { pid = ShapeOptions.PropertyId.fillShadeColors, fComplex = true, opComplex = colors };

            var sw = new StringWriter();
            using (var w = XmlWriter.Create(sw, new XmlWriterSettings { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment }))
            {
                var mapping = new FillMapping(null, w, null);
                Assert.DoesNotThrow(() => mapping.Apply(so));
            }
        }
    }
}
