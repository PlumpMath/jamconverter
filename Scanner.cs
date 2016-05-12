﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace jamconverter
{
    public class Scanner
    {
        private readonly string _input;
        private int nextChar = 0;
		private bool isInsideQuote = false;
        private bool _insideVariableExpansionModifierSpan;
        private int _insideVariableExpansionDepth = 0;

        List<ScanToken> _previouslyScannedTokens = new List<ScanToken>();

        public Scanner(string input)
        {
            _input = input;
        }

        public ScanResult Scan()
        {
            return new ScanResult(ScanAllTokens().ToArray());
        }

        public IEnumerable<ScanToken> ScanAllTokens()
        {
            while (true)
            {
                var sr = ScanToken();
                yield return sr;
                if (sr.tokenType == TokenType.EOF)
                    yield break;
                
            }
        }

        public ScanToken ScanToken()
        {
            var scanToken = ScanTokenImpl();
            this._previouslyScannedTokens.Add(scanToken);
            return scanToken;
        }

        private ScanToken ScanTokenImpl()
        {
            if (nextChar >= _input.Length)
                return new ScanToken() {literal = "", tokenType = TokenType.EOF};

            var c = _input[nextChar];

            if (_insideVariableExpansionModifierSpan)
            {
                if (c == '=')
                {
                    _insideVariableExpansionModifierSpan = false;
                    nextChar++;
                    var scanToken = new ScanToken() {tokenType = TokenType.Assignment, literal = c.ToString()};
                    return scanToken;
                }
                if (char.IsLetter(c))
                {
                    nextChar++;
                    var scanToken = new ScanToken() {tokenType = TokenType.VariableExpansionModifier, literal = c.ToString()};
                    return scanToken;
                }
				if (c == '\\' && _input.Substring(nextChar, 4) == "\\\\\\\\")
				{
					nextChar += 4;
					return new ScanToken { tokenType = TokenType.VariableExpansionModifier, literal = "\\\\\\\\" };
				}
                if (c == ')')
                    _insideVariableExpansionModifierSpan = false;
            }

            if (char.IsWhiteSpace(c))
                return new ScanToken() {tokenType = TokenType.WhiteSpace, literal = ReadWhiteSpace()};

			bool hasMoreCharacters = (nextChar + 1) < _input.Length;
			if (c == '$' && hasMoreCharacters && _input[nextChar + 1] == '(')
			{
				++_insideVariableExpansionDepth;
				nextChar += 2;
				return new ScanToken() { tokenType = TokenType.VariableDereferencerOpen, literal = "$(" };
			}

			if (c == '@' && hasMoreCharacters && _input[nextChar + 1] == '(')
			{
				++_insideVariableExpansionDepth;
				nextChar += 2;
				return new ScanToken() { tokenType = TokenType.LiteralExpansionOpen, literal = "@(" };
			}

			if (c == ')')
			{
				--_insideVariableExpansionDepth;
				++nextChar;
				return new ScanToken() { tokenType = TokenType.ParenthesisClose, literal = ")" };
			}

            if (c == '#')
            {
                ReadUntilEndOfLine();
                return ScanToken();
            }

            ////FIXME: do this properly; ReadLiteral should not recognize what isn't a literal

            var oldPosition = nextChar;

            var literal = ReadLiteral(allowColon: _insideVariableExpansionDepth==0);

            var isUnquotedLiteral = (nextChar - oldPosition) == literal.Length;
            if (isUnquotedLiteral && literal == ":" && !char.IsWhiteSpace(_input[nextChar]))
                _insideVariableExpansionModifierSpan = true;

            var tokenType = TokenType.Literal;
            if (isUnquotedLiteral)
			{
				bool hasWhitespaceBefore = (oldPosition == 0 || char.IsWhiteSpace(_input[oldPosition - 1]));
				bool hasWhitespaceAfter = (nextChar == _input.Length || char.IsWhiteSpace(_input[nextChar]));

				tokenType = TokenTypeFor(literal, hasWhitespaceAfter && hasWhitespaceBefore);
			}

            return new ScanToken() {tokenType = tokenType, literal = literal};
        }
        
        private TokenType TokenTypeFor(string literal, bool hasWhitespaceAround)
        {
            switch (literal)
            {
                case ";":
					if (hasWhitespaceAround)
						return TokenType.Terminator;
					break;
                case "[":
                    return TokenType.BracketOpen;
                case "]":
                    return TokenType.BracketClose;
                case ":":
					if (_insideVariableExpansionDepth > 0 || hasWhitespaceAround)
						return TokenType.Colon;
					break;
                case "{":
					if (hasWhitespaceAround)
						return TokenType.AccoladeOpen;
					break;
                case "}":
					if (hasWhitespaceAround)
						return TokenType.AccoladeClose;
					break;
                case "=":
					if (_insideVariableExpansionModifierSpan || hasWhitespaceAround)
						return TokenType.Assignment;
					break;
                case "if":
                    return TokenType.If;
                case "rule":
                    return TokenType.Rule;
                case "return":
                    return TokenType.Return;
                case "actions":
                    return TokenType.Actions;
				case "include":
				case "Include":
		            return TokenType.Include;
                case "on":
                    return TokenType.On;
                case "!":
                    return TokenType.Not;
                case "else":
                    return TokenType.Else;
                case "while":
                    return TokenType.While;
                case "+=":
                    return TokenType.AppendOperator;
                case "-=":
                    return TokenType.SubtractOperator;
                case "?=":
                    return TokenType.AssignmentIfEmpty;
				case "<":
					if (hasWhitespaceAround)
						return TokenType.LessThan;
					break;
				case ">":
					if (hasWhitespaceAround)
						return TokenType.GreaterThan;
					break;
                case "for":
                    return TokenType.For;
                case "in":
                    return TokenType.In;
                case "continue":
                    return TokenType.Continue;
                case "break":
                    return TokenType.Break;
                case "case":
                    return TokenType.Case;
                case "switch":
                    return TokenType.Switch;
				case "local":
		            return TokenType.Local;
				case "&&":
		            return TokenType.And;
				case "||":
		            return TokenType.Or;
				case "!=":
		            return TokenType.NotEqual;
            }
            return TokenType.Literal;
        }

        private StringBuilder _builder = new StringBuilder();
        
        private string ReadLiteral(bool allowColon)
        {
            int i;

            for (i = nextChar; i != _input.Length; i++)
            {
				//dont allow colons as the first character 
				bool reallyAllowCon = allowColon && nextChar != i; 
				bool hasMoreCharacters = (i + 1) < _input.Length;
				char ch = _input[i]; 
				if (ch == '\\' && hasMoreCharacters)
				{
					++i; 
					_builder.Append(_input[i]); 
				} 
				else if ((ch == '$' || ch == '@') && hasMoreCharacters && _input[i + 1] == '(')
				{
					Debug.Assert(i > nextChar);
					break;
				}
				else if (ch == ')' && _insideVariableExpansionDepth > 0)
				{
					Debug.Assert(i > nextChar);
					break;
				}
                else if (isInsideQuote)
                {
                    if (ch == '"')
						isInsideQuote = false;
					else 
						_builder.Append(ch);
                }
				else if (ch == '"')
				{
					isInsideQuote = true;
				}
				else if (IsLiteral(ch, reallyAllowCon) || ((ch == '$' || ch == '@') && hasMoreCharacters && _input[i + 1] != '(')) // Prevent single $ inside literal being treated as DereferenceVariable token. 
				{ 
					_builder.Append(ch); 
				} 
				else
				{
					break; 
				}
            }

			// Return special characters recognized by TokenForLiteral from here 
			// even though that doesn't make any sense. 
            if (i == nextChar)
            {
                nextChar++;
                return _input[i].ToString();
            }

            var result = _builder.ToString(); 
            _builder.Clear(); 

            nextChar = i;
            return result;
        }

        private bool IsLiteral(char c, bool treatColonAsLiteral)
        {
            if (c == '}')
                return false;
            if (c == '{')
                return false;
            if (c == ':')
                return treatColonAsLiteral;
            if (c == '[')
                return false;
            if (c == ']')
                return false;
            if (c == '#')
                return false;

            return !char.IsWhiteSpace(c);
        }

        private string ReadWhiteSpace()
        {
            bool? wasPreviousWhiteSpaceNewLine = null;
            for (int i = nextChar; i != _input.Length; i++)
            {
                bool isNewLine = IsNewLineCharacter(_input[i]);

                if (!char.IsWhiteSpace(_input[i]) || (wasPreviousWhiteSpaceNewLine.HasValue && isNewLine != wasPreviousWhiteSpaceNewLine.Value))
                {
                    var result = _input.Substring(nextChar, i - nextChar);
                    nextChar = i;
                    return result;
                }
                wasPreviousWhiteSpaceNewLine = isNewLine;
            }
            var result2 = _input.Substring(nextChar);
            nextChar = _input.Length;
            return result2;
        }

        private void ReadUntilEndOfLine()
        {
            bool inNewLineSequence = false;
            for (int i = nextChar; i != _input.Length; i++)
            {
                var c = _input[i];
                if (IsNewLineCharacter(c))
                    inNewLineSequence = true;
                else
                {
                    if (inNewLineSequence)
                    {
                        nextChar = i;
                        return;
                    }
                }
            }
            nextChar = _input.Length;
        }

        private static readonly char[] s_NewLineCharacters = {'\n', (char)0x0d, (char)0x0a};

        public static bool IsNewLineCharacter(char c)
        {
            return s_NewLineCharacters.Contains(c);
        }
    }

    public class ScanToken : IEquatable<ScanToken>
    {
        public TokenType tokenType;
        public string literal;

        public ScanToken Is(TokenType tokenType)
        {
            if (this.tokenType != tokenType)
				throw new ParsingException(string.Format("Expected token {0}, but got {1}", tokenType, this.tokenType));
            return this;
        }

	    public bool Equals(ScanToken other)
	    {
		    return (other != null && other.literal == literal && other.tokenType == tokenType);
	    }
    }

    public enum TokenType
    {
        Literal,
        Terminator,
        WhiteSpace,
        BracketClose,
        Colon,
        BracketOpen,
        VariableDereferencerOpen,
	    LiteralExpansionOpen,
		ParenthesisClose,
        Assignment,
        AccoladeOpen,
        AccoladeClose,
        If,
        Rule,
        VariableExpansionModifier,
        Return,
        AppendOperator,
        Actions,
        On,
        EOF,
        Not,
        Else,
        While,
        SubtractOperator,
        For,
        In,
        Continue,
        Break,
        Switch,
        Case,
	    Local,
	    AssignmentIfEmpty,
	    And,
	    Or,
	    NotEqual,
	    Include,
	    GreaterThan,
		LessThan
	}
}
