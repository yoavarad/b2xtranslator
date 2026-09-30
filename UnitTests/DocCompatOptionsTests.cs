using b2xtranslator.CommonTranslatorLib;
using b2xtranslator.DocFileFormat;
using b2xtranslator.WordprocessingMLMapping;
using NUnit.Framework;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;

namespace UnitTests
{
    [TestFixture]
    public class DocCompatOptionsTests
    {
        private static string WriteCompat(DocumentProperties dop)
        {
            var sw = new StringWriter();
            using (var w = XmlWriter.Create(sw, new XmlWriterSettings { OmitXmlDeclaration = true }))
            {
                var mapping = (SettingsMapping)RuntimeHelpers.GetUninitializedObject(typeof(SettingsMapping));
                typeof(AbstractOpenXmlMapping).GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mapping, w);
                typeof(SettingsMapping).GetMethod("writeCompatibilitySettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mapping, new object[] { dop });
            }
            return sw.ToString();
        }

        private static DocumentProperties NewDop() =>
            (DocumentProperties)RuntimeHelpers.GetUninitializedObject(typeof(DocumentProperties));

        [Test]
        public void UnsetBits_OmitThreeCompatElements()
        {
            string xml = WriteCompat(NewDop());
            StringAssert.DoesNotContain("doNotAutofitConstrainedTables", xml);
            StringAssert.DoesNotContain("doNotBreakConstrainedForcedTable", xml);
            StringAssert.DoesNotContain("underlineTabInNumList", xml);
        }

        [Test]
        public void SetBits_EmitThreeCompatElements()
        {
            var dop = NewDop();
            dop.fDontAutofitConstrainedTable = true;
            dop.fDontBreakConstrainedForcedTable = true;
            dop.fUnderlineTabInNumList = true;
            string xml = WriteCompat(dop);
            StringAssert.Contains("doNotAutofitConstrainedTables", xml);
            StringAssert.Contains("doNotBreakConstrainedForcedTable", xml);
            StringAssert.Contains("underlineTabInNumList", xml);
        }
    }
}
