using System.Linq;
using NUnit.Framework;

namespace jamconverter
{
    [TestFixture]
    public class ScannerTests
    {
        [Test]
        public void Simple()
        {
            var a = new Scanner("hello ;");

            var scanResult = a.Scan();
            Assert.AreEqual(TokenType.Literal, scanResult.tokenType);
            Assert.AreEqual("hello", scanResult.literal);

            var scanResult2 = a.Scan();
            Assert.AreEqual(TokenType.WhiteSpace, scanResult2.tokenType);
        }

        [Test]
        public void Invocation()
        {
            var a = new Scanner("[ MyRule arg1 : arg2 ] ;");

            var expected = new[]
            {
                new ScanResult() {tokenType = TokenType.BracketOpen, literal = "["},
                new ScanResult() {tokenType = TokenType.WhiteSpace, literal = " "},
                new ScanResult() {tokenType = TokenType.Literal, literal = "MyRule"},
                new ScanResult() {tokenType = TokenType.WhiteSpace, literal = " "},
                new ScanResult() {tokenType = TokenType.Literal, literal = "arg1"},
                new ScanResult() {tokenType = TokenType.WhiteSpace, literal = " "},
                new ScanResult() {tokenType = TokenType.ArgumentSeperator, literal = ":"},
                new ScanResult() {tokenType = TokenType.WhiteSpace, literal = " "},
                new ScanResult() {tokenType = TokenType.Literal, literal = "arg2"},
                new ScanResult() {tokenType = TokenType.WhiteSpace, literal = " "},
                new ScanResult() {tokenType = TokenType.BracketClose, literal = "]"},
                new ScanResult() {tokenType = TokenType.WhiteSpace, literal = " "},
                new ScanResult() {tokenType = TokenType.Terminator, literal = ";"},
            };

            var result = a.ScanAll().ToArray();
            
            //regular CollectionAssert.AreEqual only works with IComparer<T>
            CollectionAssert.AreEqual(expected.Select(sr => sr.tokenType).ToArray(), result.Select(sr=>sr.tokenType).ToArray());
            CollectionAssert.AreEqual(expected.Select(sr => sr.literal), result.Select(sr => sr.literal));
        }

        [Test]
        public void DereferencingVariable()
        {
            var a = new Scanner("$(myvar)");

            var scanResult1 = a.Scan();
            Assert.AreEqual(TokenType.VariableDereferencer, scanResult1.tokenType);

            var scanResult2 = a.Scan();
            Assert.AreEqual(TokenType.ParenthesisOpen, scanResult2.tokenType);

            var scanResult3 = a.Scan();
            Assert.AreEqual(TokenType.Literal, scanResult3.tokenType);
            Assert.AreEqual("myvar", scanResult3.literal);

            var scanResult4 = a.Scan();
            Assert.AreEqual(TokenType.ParenthesisClose, scanResult4.tokenType);
        }

        [Test]
        public void TwoAccolades()
        {
            var a = new Scanner("{}");
            var result = a.ScanAll().ToArray();
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual(TokenType.AccoladeOpen, result[0].tokenType);
            Assert.AreEqual(TokenType.AccoladeClose, result[1].tokenType);
        }

        [Test]
        public void TwoAccoladesWithLiteralInside()
        {
            var a = new Scanner("{ harry }");
            var result = a.ScanAll().ToArray();
            Assert.AreEqual(5, result.Length);

            CollectionAssert.AreEqual(new[] { TokenType.AccoladeOpen, TokenType.WhiteSpace, TokenType.Literal, TokenType.WhiteSpace, TokenType.AccoladeClose}, result.Select(r => r.tokenType));
        }


        [Test]
        public void LetterFollowedByDollar()
        {
            var a = new Scanner("a$");
            var result = a.ScanAll().ToArray();
            Assert.AreEqual(2, result.Length);

            CollectionAssert.AreEqual(new[] { TokenType.Literal, TokenType.VariableDereferencer }, result.Select(r => r.tokenType));
        }

    }
}