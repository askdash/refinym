using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SubtypeInduction;

namespace Tests
{
    [TestClass]
    public class SubtokenSplittingTest
    {
        [TestMethod]
        public void TestSimpleSplit()
        {
            var subtoks = SubtokenSplitter.SplitSubtokens("someSimpleTest1");
            Assert.AreEqual(subtoks[0], "some");
            Assert.AreEqual(subtoks[1], "simple");
            Assert.AreEqual(subtoks[2], "test");
            Assert.AreEqual(subtoks[3], "1");
        }

        [TestMethod]
        public void TestWithUnderscores()
        {
            var subtoks = SubtokenSplitter.SplitSubtokens("SOME_WEIRD_NAME");
            Assert.AreEqual(subtoks[0], "some");
            Assert.AreEqual(subtoks[1], "weird");
            Assert.AreEqual(subtoks[2], "name");
        }

        [TestMethod]
        public void TestWithContigiousCapitalization()
        {
            var subtoks = SubtokenSplitter.SplitSubtokens("ThisASTIsBlue");
            Assert.AreEqual(subtoks[0], "this");
            Assert.AreEqual(subtoks[1], "ast");
            Assert.AreEqual(subtoks[2], "is");
            Assert.AreEqual(subtoks[3], "blue");
        }

        [TestMethod]
        public void TestEmpty()
        {
            var subtoks = SubtokenSplitter.SplitSubtokens("");
            Assert.AreEqual(subtoks.Length, 0);
        }

        [TestMethod]
        public void TestSingleNumeric()
        {
            var subtoks = SubtokenSplitter.SplitSubtokens("str0");
            Assert.AreEqual(subtoks.Length, 2);
            Assert.AreEqual(subtoks[0], "str");
            Assert.AreEqual(subtoks[1], "0");
        }
    }
}
