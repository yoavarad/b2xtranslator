using b2xtranslator.OpenXmlLib;
using NUnit.Framework;
using System;

namespace UnitTests
{
    [TestFixture]
    public class UriEscapingTests
    {
        // Invoked via reflection so the obsolete API (SYSLIB0013) serves as the oracle without a compile warning.
        static string Legacy(string s) =>
            (string)typeof(Uri).GetMethod("EscapeUriString", new[] { typeof(string) }).Invoke(null, new object[] { s });

        [TestCase("")]
        [TestCase("http://example.com/a/b?c=d&e=f#frag")]
        [TestCase("docs/My File.doc")]
        [TestCase("..\\folder\\file name.xls")]
        [TestCase("mailto:someone@example.com?subject=Hello World")]
        [TestCase("100% done")]
        [TestCase("already%20escaped")]
        [TestCase("a<b>c\"d{e}f|g^h`i")]
        [TestCase("!#$&'()*+,/:;=?@[]-._~")]
        [TestCase("café שלום 中文")]
        [TestCase("emoji 😀 end")]
        [TestCase("lone \ud83d surrogate")]
        [TestCase("tab\tnewline\r\nnul\0")]
        public void MatchesLegacyEscapeUriString(string input)
        {
            Assert.That(UriEscaping.EscapeUriString(input), Is.EqualTo(Legacy(input)));
        }

        [Test]
        public void MatchesLegacyForEveryBmpChar()
        {
            for (int c = 0; c <= 0xFFFF; c++)
            {
                string s = "x" + (char)c + "y";
                Assert.That(UriEscaping.EscapeUriString(s), Is.EqualTo(Legacy(s)), $"U+{c:X4}");
            }
        }

        [Test]
        public void NullThrows()
        {
            Assert.Throws<ArgumentNullException>(() => UriEscaping.EscapeUriString(null));
        }
    }
}
