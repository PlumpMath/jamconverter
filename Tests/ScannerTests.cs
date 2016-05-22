using System.Linq;
using System.Text;
using NUnit.Framework;

namespace jamconverter.Tests
{
    [TestFixture]
    public class ScannerTests
    {
        [Test]
        public void Simple()
        {
            var a = new Scanner("hello ;");

            var scanResult = a.ScanToken();
            Assert.AreEqual(TokenType.Literal, scanResult.tokenType);
            Assert.AreEqual("hello", scanResult.literal);

            var scanResult2 = a.ScanToken();
            Assert.AreEqual(TokenType.WhiteSpace, scanResult2.tokenType);
        }

        [Test]
        public void Invocation()
        {
            var a = new Scanner("[ MyRule arg1 : arg2 ] ;");

            var expected = new[]
            {
                new ScanToken() {tokenType = TokenType.BracketOpen, literal = "["},
                new ScanToken() {tokenType = TokenType.WhiteSpace, literal = " "},
                new ScanToken() {tokenType = TokenType.Literal, literal = "MyRule"},
                new ScanToken() {tokenType = TokenType.WhiteSpace, literal = " "},
                new ScanToken() {tokenType = TokenType.Literal, literal = "arg1"},
                new ScanToken() {tokenType = TokenType.WhiteSpace, literal = " "},
                new ScanToken() {tokenType = TokenType.Colon, literal = ":"},
                new ScanToken() {tokenType = TokenType.WhiteSpace, literal = " "},
                new ScanToken() {tokenType = TokenType.Literal, literal = "arg2"},
                new ScanToken() {tokenType = TokenType.WhiteSpace, literal = " "},
                new ScanToken() {tokenType = TokenType.BracketClose, literal = "]"},
                new ScanToken() {tokenType = TokenType.WhiteSpace, literal = " "},
                new ScanToken() {tokenType = TokenType.Terminator, literal = ";"},
                new ScanToken() {tokenType = TokenType.EOF, literal = ""},
            };

            var result = a.ScanAllTokens().ToArray();
            
            //regular CollectionAssert.AreEqual only works with IComparer<T>
            CollectionAssert.AreEqual(expected.Select(sr => sr.tokenType).ToArray(), result.Select(sr=>sr.tokenType).ToArray());
            CollectionAssert.AreEqual(expected.Select(sr => sr.literal), result.Select(sr => sr.literal));
        }

        [Test]
        public void DereferencingVariable()
        {
            var a = new Scanner("$(myvar)");

            var scanResult1 = a.ScanToken();
            Assert.AreEqual(TokenType.VariableDereferencerOpen, scanResult1.tokenType);

			var scanResult2 = a.ScanToken();
            Assert.AreEqual(TokenType.Literal, scanResult2.tokenType);
            Assert.AreEqual("myvar", scanResult2.literal);

			var scanResult3 = a.ScanToken();
            Assert.AreEqual(TokenType.ParenthesisClose, scanResult3.tokenType);
        }

        [Test]
        public void TwoAccolades()
        {
            var a = new Scanner("{ }");
            var result = a.ScanAllTokens().ToArray();
            Assert.AreEqual(4, result.Length);
            Assert.AreEqual(TokenType.AccoladeOpen, result[0].tokenType);
            Assert.AreEqual(TokenType.AccoladeClose, result[2].tokenType);
			Assert.AreEqual(TokenType.WhiteSpace, result[1].tokenType);
            Assert.AreEqual(TokenType.EOF, result[3].tokenType);
        }

        [Test]
        public void TwoAccoladesWithLiteralInside()
        {
            var a = new Scanner("{ harry }");
            var result = a.ScanAllTokens().ToArray();

            CollectionAssert.AreEqual(new[] { TokenType.AccoladeOpen, TokenType.WhiteSpace, TokenType.Literal, TokenType.WhiteSpace, TokenType.AccoladeClose, TokenType.EOF}, result.Select(r => r.tokenType));
        }

        [Test]
        public void QuotedExpression()
        {
            var a = new Scanner(@"""($(d)) = """);
            var result = a.ScanAllTokens().ToArray();

            CollectionAssert.AreEqual(new[] { TokenType.Literal, TokenType.VariableDereferencerOpen, TokenType.Literal, TokenType.ParenthesisClose, TokenType.Literal, TokenType.EOF }, result.Select(r => r.tokenType));
        }

        [Test]
        public void VariableDereferenceWithIndexerInsideQuotes()
        {
            //"($(passed_define_set[0])=.*)\\s*"
            var a = new Scanner(@"""($(passed_define_set[0]) =.*)""");
            var result = a.ScanAllTokens().ToArray();

            Assert.AreEqual("passed_define_set", result[2].literal);
       }

        [Test]
        public void EscapedEndQuote()
        {
            var sb = new StringBuilder();
            sb.Append('"');
            sb.Append('a');
            sb.Append('\\');
            sb.Append('"');
            sb.Append('"');

            var a = new Scanner(sb.ToString());
            var result = a.ScanAllTokens().ToArray();

            Assert.AreEqual("a\"", result[0].literal);
            Assert.AreEqual(2, result.Length);
        }

        [Test]
        public void LetterFollowedByDollar()
        {
            var a = new Scanner("a$");
            var result = a.ScanAllTokens().ToArray();

            CollectionAssert.AreEqual(new[] { TokenType.Literal, TokenType.EOF }, result.Select(r => r.tokenType));
			Assert.That(result[0].literal, Is.EqualTo("a$"));
        }


        [Test]
        public void VariableExpansionModifier()
        {
            var a = new Scanner("$(harry:BS");
            var result = a.ScanAllTokens().ToArray();

            CollectionAssert.AreEqual(new[] { TokenType.VariableDereferencerOpen, TokenType.Literal, TokenType.Colon, TokenType.VariableExpansionModifier, TokenType.VariableExpansionModifier, TokenType.EOF }, result.Select(r => r.tokenType));

            Assert.AreEqual("B", result[3].literal);
            Assert.AreEqual("S", result[4].literal);
        }

        [Test]
        public void VariableExpansionModifierWithValue()
        {
            var a = new Scanner("$(harry:BS=v");
            var result = a.ScanAllTokens().ToArray();

            CollectionAssert.AreEqual(new[] { TokenType.VariableDereferencerOpen, TokenType.Literal, TokenType.Colon, TokenType.VariableExpansionModifier, TokenType.VariableExpansionModifier, TokenType.Assignment, TokenType.Literal, TokenType.EOF }, result.Select(r => r.tokenType));
            
            Assert.AreEqual("v", result[6].literal);
        }

		[Test]
		public void VariableExpansionWithComparisonOperators()
		{
			var a = new Scanner("$(<) $(>)");
			var result = a.ScanAllTokens().ToArray();

			Assert.That(result.Length, Is.EqualTo(8));
			Assert.That(result[0].tokenType, Is.EqualTo(TokenType.VariableDereferencerOpen));
			Assert.That(result[0].literal, Is.EqualTo("$("));
			Assert.That(result[1].tokenType, Is.EqualTo(TokenType.Literal));
			Assert.That(result[1].literal, Is.EqualTo("<"));
			Assert.That(result[5].literal, Is.EqualTo(">"));
		}

        [Test]
        public void DontCombineNewLineAndWhiteSpaceInSingleToken()
        {
            
            var a = new Scanner(
/* note that in addition to the newline, there is whitepsace after hello, and before there*/
@"hello  
  there");
            var result = a.ScanAllTokens().ToArray();

            CollectionAssert.AreEqual(new[] { TokenType.Literal, TokenType.WhiteSpace, TokenType.WhiteSpace, TokenType.WhiteSpace, TokenType.Literal, TokenType.EOF }, result.Select(r => r.tokenType));
        }

        [Test]
        public void Comment()
        {
            var a = new Scanner(
@"hello #this means hello
on_new_line");
            var result = a.ScanAllTokens().ToArray();

            //we do not expect the comment to be reported by the scanner
            CollectionAssert.AreEqual(new[] { TokenType.Literal, TokenType.WhiteSpace, TokenType.Literal, TokenType.EOF}, result.Select(r => r.tokenType));
        }

        [Test]
        public void LiteralContainingColon()
        {
            var a = new Scanner("hello:there");
            var result = a.ScanAllTokens().ToArray();

            CollectionAssert.AreEqual(new[] { TokenType.Literal, TokenType.EOF }, result.Select(r => r.tokenType));

            Assert.AreEqual("hello:there", result[0].literal);
        }

	    [Test]
	    public void LiteralContainingBackslash()
	    {
		    var scanner = new Scanner(@"a\ b   a\\b    a\bb   a\n\r\t\bc");
		    var result = scanner.ScanAllTokens().ToArray();

			Assert.That(result.Length, Is.EqualTo(8));
			Assert.That(result[0].literal, Is.EqualTo("a b"));
			Assert.That(result[2].literal, Is.EqualTo(@"a\b"));
			Assert.That(result[4].literal, Is.EqualTo("abb"));
			Assert.That(result[6].literal, Is.EqualTo("anrtbc"));
	    }

	    [Test]
	    public void LiteralContainingEscapedDollarSign()
	    {
		    var scanner1 = new Scanner(@"a\$b");
			var scanner2 = new Scanner(@"a$b");

			var result1 = scanner1.ScanAllTokens().ToArray();
			var result2 = scanner2.ScanAllTokens().ToArray();

			CollectionAssert.AreEqual(result1, result2);
	    }

		[Test]
		public void LiteralEscapedCharacters()
		{
			var a = new Scanner("\\=");
			var result = a.ScanAllTokens().ToArray();

            CollectionAssert.AreEqual(new[] { TokenType.Literal, TokenType.EOF }, result.Select(r => r.tokenType));

            Assert.AreEqual("=", result[0].literal);
        }

        [Test]
        public void QuotedLiteral()
        {
            var a = new Scanner("\"hello there\"");
            var result = a.ScanAllTokens().ToArray();

            CollectionAssert.AreEqual(new[] { TokenType.Literal, TokenType.EOF }, result.Select(r => r.tokenType));

            Assert.AreEqual("hello there", result[0].literal);
        }

        [Test]
        public void QuotedLiteralContainingEscapedQuote()
        {
            var a = new Scanner("\"hello \\\"there\"");
            var result = a.ScanAllTokens().ToArray();

            CollectionAssert.AreEqual(new[] { TokenType.Literal, TokenType.EOF }, result.Select(r => r.tokenType));

            Assert.AreEqual("hello \"there", result[0].literal);
        }

		[Test]
		public void SmallerThanInLiteral()
		{
			var a = new Scanner("<you<can>do>this>");
			var result = a.ScanAllTokens().ToArray();

			CollectionAssert.AreEqual(new[] { TokenType.Literal, TokenType.EOF }, result.Select(r => r.tokenType));

			Assert.AreEqual("<you<can>do>this>", result[0].literal);
		}

		[Test]
		public void ParenthesisIsNotSpecial()
		{
			var a = new Scanner("(aa)");
			var result = a.ScanAllTokens().ToArray();

			Assert.That(result.Length, Is.EqualTo(2));
			Assert.That(result[0].literal, Is.EqualTo("(aa)"));
		}



        [Test]
        public void ParenthesisAreOk()
        {
            var a = new Scanner("( ) && $(c)");
            var result = a.ScanAllTokens().ToArray();
            
            Assert.That(result.Last().tokenType == TokenType.EOF);

            Assert.That(result[result.Length-3].literal, Is.EqualTo("c"));
        }

        [Test]
		public void LiteralExpansion()
		{
			var a = new Scanner("@(abc)");
			var result = a.ScanAllTokens().ToArray();

			Assert.That(result.Length, Is.EqualTo(4));
			Assert.That(result[0].literal, Is.EqualTo("@("));
			Assert.That(result[1].literal, Is.EqualTo("abc"));
			Assert.That(result[2].literal, Is.EqualTo(")"));
		}
	}
}
