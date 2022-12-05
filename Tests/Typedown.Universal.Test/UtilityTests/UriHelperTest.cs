using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Typedown.Core.Utilities;

namespace Typedown.Core.Test.UtilityTests
{
    [TestClass]
    public class UriHelperTest
    {
        [TestMethod]
        [DataTestMethod]
        [DataRow("example/1.jpg")]
        [DataRow("C:/example/1.jpg")]
        [DataRow("file:///C:/example/1.jpg")]
        public void IsWebUrl_WithNotWebUrl_ReturnFalse(string str)
        {
            Assert.IsFalse(UriHelper.IsWebUrl(str));
        }

        [TestMethod]
        [DataTestMethod]
        [DataRow("http://www.example.com/1.jpg")]
        [DataRow("https://www.example.com/1.jpg")]
        public void IsWebUrl_WithWebUrl_ReturnTrue(string str)
        {
            Assert.IsTrue(UriHelper.IsWebUrl(str));
        }

        [TestMethod]
        [DataTestMethod]
        [DataRow("example")]
        [DataRow("C:/example")]
        [DataRow("http://www.example.com/1.jpg")]
        [DataRow("https://www.example.com/1.jpg")]
        public void IsWebUrl_WithNotLocalUrl_ReturnFalse(string str)
        {
            Assert.IsFalse(UriHelper.IsLocalUrl(str));
        }

        [TestMethod]
        [DataTestMethod]
        [DataRow("file:///C:/example/1.jpg")]
        public void IsWebUrl_WithLocalUrl_ReturnTrue(string str)
        {
            Assert.IsTrue(UriHelper.IsLocalUrl(str));
        }

        [TestMethod]
        [DataTestMethod]
        [DataRow("http://www.example.com/1.jpg")]
        [DataRow("https://www.example.com/1.jpg")]
        [DataRow("example/1.jpg")]
        [DataRow("file:///C:/example/1.jpg")]
        public void IsWebUrl_WithNotAbsolutePath_ReturnFalse(string str)
        {
            Assert.IsFalse(UriHelper.IsAbsolutePath(str));
        }

        [TestMethod]
        [DataTestMethod]
        [DataRow("C:/example/1.jpg")]
        [DataRow("C:\\example\\1.jpg")]
        [DataRow("\\\\example\\1.jpg")]
        public void IsWebUrl_WithAbsolutePath_ReturnTruestring(string str)
        {
            Assert.IsTrue(UriHelper.IsAbsolutePath(str));
        }
    }
}
