using System.IO;
using System.Xml;
using NUnit.Framework;
using PptUtils = b2xtranslator.PresentationMLMapping.Utils;

namespace UnitTests
{
    [TestFixture]
    public class PptClrMapOvrTests
    {
        private const string P = "http://schemas.openxmlformats.org/presentationml/2006/main";
        private const string A = "http://schemas.openxmlformats.org/drawingml/2006/main";

        private static XmlDocument Write(bool own, XmlElement map)
        {
            var sw = new StringWriter();
            using (var w = XmlWriter.Create(sw, new XmlWriterSettings { OmitXmlDeclaration = true }))
            {
                PptUtils.WriteClrMapOvr(w, own, map);
            }
            var doc = new XmlDocument();
            doc.LoadXml(sw.ToString());
            return doc;
        }

        [Test]
        public void NoOverride_WritesMasterClrMapping()
        {
            var doc = Write(false, null);
            Assert.That(doc.DocumentElement.LocalName, Is.EqualTo("clrMapOvr"));
            Assert.That(doc.DocumentElement.NamespaceURI, Is.EqualTo(P));
            Assert.That(doc.DocumentElement.FirstChild.LocalName, Is.EqualTo("masterClrMapping"));
            Assert.That(doc.DocumentElement.FirstChild.NamespaceURI, Is.EqualTo(A));
        }

        [Test]
        public void Override_WithMapping_CopiesAttributes()
        {
            var src = new XmlDocument();
            src.LoadXml("<a:clrMap xmlns:a=\"" + A + "\" bg1=\"dk2\" tx1=\"lt1\"/>");
            var doc = Write(true, src.DocumentElement);
            var ov = (XmlElement)doc.DocumentElement.FirstChild;
            Assert.That(ov.LocalName, Is.EqualTo("overrideClrMapping"));
            Assert.That(ov.GetAttribute("bg1"), Is.EqualTo("dk2"));
            Assert.That(ov.GetAttribute("tx1"), Is.EqualTo("lt1"));
        }

        [Test]
        public void Override_WithoutMapping_WritesMasterClrMapping()
        {
            Assert.That(Write(true, null).DocumentElement.FirstChild.LocalName, Is.EqualTo("masterClrMapping"));
        }

        [Test]
        public void Override_PartialMapping_FillsMissingWithIdentity()
        {
            var src = new XmlDocument();
            src.LoadXml("<a:clrMap xmlns:a=\"" + A + "\" bg1=\"dk2\"/>");
            var ov = (XmlElement)Write(true, src.DocumentElement).DocumentElement.FirstChild;
            Assert.That(ov.GetAttribute("accent6"), Is.EqualTo("accent6"));
            Assert.That(ov.GetAttribute("tx2"), Is.EqualTo("dk2"));
        }

        [TestCase((ushort)0x0, true)]
        [TestCase((ushort)0x2, false)]
        [TestCase((ushort)0x5, true)]
        public void HasOwnColorScheme_ReadsFMasterScheme(ushort flags, bool expected)
        {
            Assert.That(PptUtils.HasOwnColorScheme(flags), Is.EqualTo(expected));
        }

        [TestCase((ushort)0x0, true)]
        [TestCase((ushort)0x4, false)]
        [TestCase((ushort)0x3, true)]
        public void HasOwnBackground_ReadsFMasterBackground(ushort flags, bool expected)
        {
            Assert.That(PptUtils.HasOwnBackground(flags), Is.EqualTo(expected));
        }
    }
}
