﻿using GitCommands;

namespace GitCommandsTests
{
    [TestFixture]
    public class PathEqualityComparerTests
    {
        private PathEqualityComparer _comparer;

        [SetUp]
        public void Setup()
        {
            _comparer = new PathEqualityComparer();
        }

        [TestCase("C:\\WORK\\GitExtensions\\", "C:/Work/GitExtensions/")]
        [TestCase("\\\\my-pc\\Work\\GitExtensions\\", "//my-pc/WORK/GitExtensions/")]
        public void Equals(string input, string expected)
        {
            ClassicAssert.AreEqual(_comparer.Equals(input, expected), true);
        }
    }
}
