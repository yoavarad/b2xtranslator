using b2xtranslator.PptFileFormat;
using b2xtranslator.PresentationMLMapping;
using NUnit.Framework;
using System;
using System.IO;
using System.Text;
using System.Xml;

namespace UnitTests
{
    [TestFixture]
    public class PptSlideTransitionTests
    {
        private const short ManualAdvance = 0x1;
        private const short AutoAdvance = 0x1 << 10;

        private static SlideShowSlideInfoAtom Atom(byte effectType, byte effectDirection, short flags, int slideTime = 0, byte speed = 2)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(slideTime);
            w.Write(0u); // soundIdRef
            w.Write(effectDirection);
            w.Write(effectType);
            w.Write(flags);
            w.Write(speed);
            w.Write(new byte[3]); // unused
            w.Flush();
            ms.Position = 0;
            return new SlideShowSlideInfoAtom(new BinaryReader(ms), (uint)ms.Length, 1017, 0, 0);
        }

        private static string Map(SlideShowSlideInfoAtom atom)
        {
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment };
            using (var writer = XmlWriter.Create(sb, settings))
            {
                // wrapper element so the fragment carries the p: namespace declaration once
                writer.WriteStartElement("p", "root", "http://schemas.openxmlformats.org/presentationml/2006/main");
                var type = typeof(Converter).Assembly.GetType("b2xtranslator.PresentationMLMapping.SlideTransitionMapping", true);
                object mapping =Activator.CreateInstance(type, new object[] { null, writer });
                type.GetMethod("Apply").Invoke(mapping, new object[] { atom });
                writer.WriteEndElement();
            }
            return sb.ToString();
        }

        [Test]
        public void Uncover_MapsToPull()
        {
            string xml = Map(Atom(7, 0, ManualAdvance));
            StringAssert.Contains("<p:pull dir=\"l\" />", xml);
            StringAssert.DoesNotContain("p:push", xml);
        }

        [Test]
        public void AlphaFade_MapsToFade()
        {
            StringAssert.Contains("<p:fade />", Map(Atom(23, 0, ManualAdvance)));
        }

        [Test]
        public void ManualAdvanceTransition_IsWrittenWithoutAdvTm()
        {
            string xml = Map(Atom(1, 1, ManualAdvance));
            StringAssert.Contains("<p:transition spd=\"fast\"><p:random /></p:transition>", xml);
        }

        [Test]
        public void PlainCut_WithoutAutoAdvance_WritesNothing()
        {
            StringAssert.DoesNotContain("p:transition", Map(Atom(0, 2, ManualAdvance)));
        }

        [Test]
        public void AutoAdvanceWithoutClick_WritesAdvTmAndAdvOnClick()
        {
            string xml = Map(Atom(0, 0, AutoAdvance, slideTime: 3000));
            StringAssert.Contains("advOnClick=\"0\"", xml);
            StringAssert.Contains("advTm=\"3000\"", xml);
        }

        [Test]
        public void CutThroughBlack_IsWritten()
        {
            StringAssert.Contains("<p:cut thruBlk=\"true\" />", Map(Atom(0, 1, ManualAdvance)));
        }

        [Test]
        public void AutoAndClickAdvance_WritesAdvTmOnly()
        {
            string xml = Map(Atom(6, 0, (short)(ManualAdvance | AutoAdvance), slideTime: 1500));
            StringAssert.Contains("advTm=\"1500\"", xml);
            StringAssert.DoesNotContain("advOnClick", xml);
        }

        [Test]
        public void DefaultDocuments_AreEmbedded()
        {
            Assert.IsNotNull(Utils.GetDefaultDocument("theme").DocumentElement);
            Assert.IsNotNull(Utils.GetDefaultDocument("slideLayouts.blank").DocumentElement);
        }
    }
}
