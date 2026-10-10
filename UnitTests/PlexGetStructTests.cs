using b2xtranslator.DocFileFormat;
using NUnit.Framework;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UnitTests
{
    [TestFixture]
    public class PlexGetStructTests
    {
        private static Plex<short> NewPlex(int[] cps, short[] elements)
        {
            // Bypass the stream-reading constructor; GetStruct only uses the two public lists.
            var plex = (Plex<short>)RuntimeHelpers.GetUninitializedObject(typeof(Plex<short>));
            plex.CharacterPositions = new List<int>(cps);
            plex.Elements = new List<short>(elements);
            return plex;
        }

        [Test]
        public void FirstCp_ReturnsFirstElement() =>
            Assert.AreEqual(10, NewPlex(new[] { 0, 5, 9 }, new short[] { 10, 20 }).GetStruct(0));

        [Test]
        public void MiddleCp_ReturnsMatchingElement() =>
            Assert.AreEqual(20, NewPlex(new[] { 0, 5, 9, 12 }, new short[] { 10, 20, 30 }).GetStruct(5));

        [Test]
        public void LastCp_HasNoElement_ReturnsDefault() =>
            Assert.AreEqual(0, NewPlex(new[] { 0, 5, 9 }, new short[] { 10, 20 }).GetStruct(9));

        [Test]
        public void CpBetweenEntries_ReturnsDefault() =>
            Assert.AreEqual(0, NewPlex(new[] { 0, 5, 9 }, new short[] { 10, 20 }).GetStruct(3));

        [Test]
        public void CpOutOfRange_ReturnsDefault()
        {
            var plex = NewPlex(new[] { 0, 5, 9 }, new short[] { 10, 20 });
            Assert.AreEqual(0, plex.GetStruct(-1));
            Assert.AreEqual(0, plex.GetStruct(100));
        }

        [Test]
        public void EmptyPlex_ReturnsDefault() =>
            Assert.AreEqual(0, NewPlex(new int[0], new short[0]).GetStruct(0));

        [Test]
        public void DuplicateCps_ReturnFirstMatch() =>
            Assert.AreEqual(20, NewPlex(new[] { 0, 5, 5, 5, 9 }, new short[] { 10, 20, 30, 40 }).GetStruct(5));
    }
}
