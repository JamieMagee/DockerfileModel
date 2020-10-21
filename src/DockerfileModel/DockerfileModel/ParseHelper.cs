﻿using System;
using System.Collections.Generic;
using System.Linq;
using DockerfileModel.Tokens;
using Sprache;

namespace DockerfileModel
{
    internal static class ParseHelper
    {
        private const char SingleQuote = '\'';
        private const char DoubleQuote = '\"';
        private static readonly char[] Quotes = new char[] { SingleQuote, DoubleQuote };

        /// <summary>
        /// Parsers for all of the variable substitution modifiers.
        /// </summary>
        private static readonly Parser<string>[] variableSubstitutionModifiers =
            VariableRefToken.ValidModifiers
                .Select(modifier => Parse.String(modifier).Text())
                .ToArray();

        /// <summary>
        /// Filters out null items from an enumerable.
        /// </summary>
        /// <typeparam name="T">Type of items contained in the enumerable.</typeparam>
        /// <param name="items">The enumerable to filter nulls from.</param>
        /// <returns>An enumerable with no null items.</returns>
        public static IEnumerable<T> FilterNulls<T>(IEnumerable<T?>? items) where T : class
        {
            if (items is null)
            {
                yield break;
            }

            foreach (T? item in items.Where(item => item != null))
            {
                yield return item!;
            }
        }

        /// <summary>
        /// Parses whitespace.
        /// </summary>
        /// <returns>Set of tokens representing whitespace.</returns>
        public static Parser<IEnumerable<Token>> Whitespace() =>
            from whitespace in Parse.WhiteSpace.Except(Parse.LineTerminator).XMany().Text()
            from newLine in OptionalNewLine()
            select ConcatTokens(
                whitespace.Length > 0 ? new WhitespaceToken(whitespace) : null,
                newLine);

        /// <summary>
        /// Parses the text of a comment, including leading whitespace.
        /// </summary>
        /// <returns>Set of tokens representing comment text.</returns>
        public static Parser<IEnumerable<Token>> CommentText() =>
            from leading in Whitespace()
            from comment in CommentToken.GetParser()
            from lineEnd in OptionalNewLine().AsEnumerable()
            select ConcatTokens(leading, new Token[] { new CommentToken(comment) }, lineEnd);

        /// <summary>
        /// Concatenates sets of tokens into a single set, removing any nulls.
        /// </summary>
        /// <param name="tokens">Sets of tokens.</param>
        /// <returns>Concatenation of all tokens.</returns>
        public static IEnumerable<Token> ConcatTokens(params Token?[]? tokens) =>
            FilterNulls(tokens).ToList();

        /// <summary>
        /// Concatenates sets of tokens into a single set, removing any nulls.
        /// </summary>
        /// <param name="tokens">Sets of tokens.</param>
        /// <returns>Concatenation of all tokens.</returns>
        public static IEnumerable<Token> ConcatTokens(params IEnumerable<Token?>[] tokens) =>
            ConcatTokens(
                FilterNulls(tokens)
                    .SelectMany(tokens => tokens)
                    .ToArray());

        /// <summary>
        /// Parses identifiers, delimited by a character.
        /// </summary>
        /// <param name="delimiter">Character which delimits segments of the string.</param>
        /// <param name="minimumDelimiters">Minimum number of delimiter characters that must exist in the string.</param>
        /// <returns>Delimited identifiers.</returns>
        public static Parser<string> DelimitedIdentifier(char delimiter, int minimumDelimiters = 0) =>
            from segments in Parse.Identifier(Parse.LetterOrDigit, Parse.LetterOrDigit).Many().DelimitedBy(Parse.Char(delimiter))
            where (segments.Count() > minimumDelimiters)
            select String.Join(delimiter.ToString(), segments.SelectMany(segment => segment).ToArray());

        /// <summary>
        /// Parses a new line that is optional.
        /// </summary>
        /// <returns>The new line token if a new line exists; otherwise, null.</returns>
        public static Parser<NewLineToken> OptionalNewLine() =>
            from lineEnd in Parse.LineEnd.Optional()
            select lineEnd.IsDefined ? new NewLineToken(lineEnd.Get()) : null;

        /// <summary>
        /// Parses a token and any trailing whitespace.
        /// </summary>
        /// <param name="parser">Token parser.</param>
        /// <returns>Set of parsed tokens.</returns>
        public static Parser<IEnumerable<Token>> TokenWithTrailingWhitespace(Parser<Token> parser) =>
            from token in parser.AsEnumerable()
            from trailingWhitespace in Whitespace()
            select ConcatTokens(token, trailingWhitespace);

        /// <summary>
        /// Parses a token and any trailing whitespace.
        /// </summary>
        /// <param name="createToken">Delegate to create the token.</param>
        /// <returns>Set of parsed tokens.</returns>
        public static Parser<IEnumerable<Token>> TokenWithTrailingWhitespace(Func<string, Token> createToken) =>
            from val in Parse.AnyChar.Except(Parse.LineEnd).Many().Text()
            select ConcatTokens(createToken(val.Trim()), GetTrailingWhitespaceToken(val)!);

        /// <summary>
        /// Returns a whitespace token for any trailing whitespace in the given string.
        /// </summary>
        /// <param name="text">String to parse.</param>
        public static WhitespaceToken? GetTrailingWhitespaceToken(string text)
        {
            string? whitespace = new string(
                text
                    .Reverse()
                    .TakeWhile(ch => Char.IsWhiteSpace(ch))
                    .Reverse()
                    .ToArray());

            if (whitespace == String.Empty)
            {
                return null;
            }

            return new WhitespaceToken(whitespace);
        }

        /// <summary>
        /// Tokenizes an argument of an instruction. This handles the parsing of whitespace and line continuations.
        /// </summary>
        /// <param name="tokenParser">Parser for the argument.</param>
        /// <param name="escapeChar">Escape character.</param>
        /// <returns>Set of tokens.</returns>
        public static Parser<IEnumerable<Token>> ArgTokens(Parser<IEnumerable<Token>> tokenParser, char escapeChar) =>
            WithTrailingComments(
                from leadingWhitespace in Whitespace()
                from token in tokenParser
                from trailingWhitespace in Whitespace().Optional()
                from lineContinuation in LineContinuation(escapeChar).Optional()
                from lineEnd in OptionalNewLine().AsEnumerable()
                select ConcatTokens(
                    leadingWhitespace,
                    token,
                    trailingWhitespace.GetOrDefault(),
                    new Token[] { lineContinuation.GetOrDefault() },
                    lineEnd));

        /// <summary>
        /// Parses a line continuation, consisting of an escape character followed by a new line.
        /// </summary>
        /// <param name="escapeChar">Escape character.</param>
        /// <returns>Line continuation tokens.</returns>
        public static Parser<LineContinuationToken> LineContinuation(char escapeChar) =>
           from escape in Symbol(escapeChar.ToString())
           from whitespace in Parse.WhiteSpace.Except(Parse.LineEnd).Many()
           from lineEnding in Parse.LineEnd
           select new LineContinuationToken(ConcatTokens(
               escape,
               whitespace.Any() ? new WhitespaceToken(new string(whitespace.ToArray())) : null,
               new NewLineToken(lineEnding)));

        /// <summary>
        /// Parses an instruction name.
        /// </summary>
        /// <param name="instructionName">Name of the instruction.</param>
        /// <param name="escapeChar">Escape character.</param>
        /// <returns>Token for the instruction name.</returns>
        public static Parser<KeywordToken> InstructionIdentifier(string instructionName, char escapeChar)
        {
            Parser<IEnumerable<Token>>? parser = null;
            for (int i = 0; i < instructionName.Length; i++)
            {
                int currentIndex = i;
                if (parser is null)
                {
                    parser = ToStringTokens(Parse.IgnoreCase(instructionName[currentIndex]));
                }
                else
                {
                    parser = from previousTokens in parser
                             from nextTokens in CharWithOptionalLineContinuation(escapeChar, Parse.IgnoreCase(instructionName[currentIndex]))
                             select ConcatTokens(previousTokens, nextTokens);
                }
            }

            return from tokens in parser
                select new KeywordToken(TokenHelper.CollapseStringTokens(tokens));
        }

        /// <summary>
        /// Parses a single character preceded by an optional line continuation.
        /// </summary>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="charParser">Character parser.</param>
        /// <returns>Parsed tokens.</returns>
        private static Parser<IEnumerable<Token>> CharWithOptionalLineContinuation(char escapeChar, Parser<char> charParser) =>
            from lineContinuation in LineContinuation(escapeChar).Optional()
            from ch in charParser
            select ConcatTokens(lineContinuation.GetOrDefault(), new StringToken(ch.ToString()));

        /// <summary>
        /// Parses an instruction.
        /// </summary>
        /// <param name="instructionName">Name of the instruction.</param>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="instructionArgsParser">Parser for the instruction's arguments.</param>
        /// <returns>Set of tokens.</returns>
        public static Parser<IEnumerable<Token>> Instruction(string instructionName, char escapeChar, Parser<IEnumerable<Token>> instructionArgsParser) =>
            from instructionNameTokens in InstructionNameWithTrailingContent(instructionName, escapeChar)
            from instructionArgs in instructionArgsParser
            select ConcatTokens(instructionNameTokens, instructionArgs);

        /// <summary>
        /// Parses a symbol.
        /// </summary>
        /// <param name="value">Symbol value.</param>
        /// <returns>A symbol token.</returns>
        public static Parser<SymbolToken> Symbol(string value) =>
            from val in Parse.String(value).Text()
            select new SymbolToken(val);

        /// <summary>
        /// Concatenates a set of string parsers with an 'or' operator.
        /// </summary>
        /// <param name="parsers">Set of string parsers to concatenate.</param>
        /// <returns>String parser that matches on any of the given parsers.</returns>
        public static Parser<string> OrConcat(params Parser<string>[] parsers) =>
            from vals in parsers.Aggregate((current, next) => current.Or(next)).Many()
            select String.Concat(vals);

        /// <summary>
        /// Concatenates a set of token parsers with an 'or' operator.
        /// </summary>
        /// <param name="parsers">Set of string parsers to concatenate.</param>
        /// <returns>String parser that matches on any of the given parsers.</returns>
        private static Parser<IEnumerable<Token>> OrConcat(params Parser<IEnumerable<Token>>[] parsers) =>
            from vals in (parsers.Aggregate((current, next) => current.Or(next))).Many()
            select vals.SelectMany(val => val);

        /// <summary>
        /// Excludes parsing of the specified characters from a parser.
        /// </summary>
        /// <param name="parser">The character parser to apply the exclusion to.</param>
        /// <param name="chars">Set of characters to be excluded from parsing.</param>
        /// <returns>Character parser that excludes the specified characters.</returns>
        private static Parser<char> ExceptChars(this Parser<char> parser, IEnumerable<char> chars) =>
            chars
                .Select(ch => Parse.Char(ch))
                .Aggregate(parser, (current, next) => current.Except(next));

        /// <summary>
        /// Parses the first letter of an argument reference.
        /// </summary>
        public static Parser<char> ArgRefFirstLetterParser => Parse.Letter;

        /// <summary>
        /// Parses the tail characters of an argument reference.
        /// </summary>
        public static Parser<char> ArgRefTailParser => Parse.LetterOrDigit.Or(Parse.Char('_'));

        /// <summary>
        /// Parses an identifier token.
        /// </summary>
        /// <param name="firstCharacterParser">Parser of the first character of the identifier.</param>
        /// <param name="tailCharacterParser">Parser of the rest of the characters of the identifier.</param>
        /// <param name="escapeChar">Escape character.</param>
        /// <returns>Parser for an identifier token.</returns>
        public static Parser<IdentifierToken> IdentifierToken(Parser<char> firstCharacterParser, Parser<char> tailCharacterParser, char escapeChar) =>
            WrappedInOptionalQuotes<IdentifierToken>(
                (char escapeChar, IEnumerable<char> excludedChars, TokenWrapper tokenWrapper) =>
                    from identifier in WrappedInQuotesIdentifier(escapeChar, firstCharacterParser, tailCharacterParser)
                    select new IdentifierToken(identifier)
                    {
                        QuoteChar = tokenWrapper.OpeningString[0]
                    },
                (char escapeChar, IEnumerable<char> excludedChars) =>
                    from identifier in IdentifierString(escapeChar, firstCharacterParser, tailCharacterParser)
                    select new Token[] { new IdentifierToken(identifier) },
                escapeChar,
                Enumerable.Empty<char>())
                .Single()
                .Cast<Token, IdentifierToken>();

        /// <summary>
        /// Parses a literal token.
        /// </summary>
        /// <param name="escapeChar">Escape character.</param>
        public static Parser<LiteralToken> LiteralToken(char escapeChar) =>
            WrappedInOptionalQuotes<LiteralToken>(
                (char escapeChar, IEnumerable<char> excludedChars, TokenWrapper tokenWrapper) =>
                    from literal in LiteralString(escapeChar, excludedChars.Concat(Quotes), isWhitespaceAllowed: true, treatWhitespaceAsLiteral: true)
                        .Many()
                        .Flatten()
                    select new LiteralToken(TokenHelper.CollapseStringTokens(literal))
                    {
                        QuoteChar = tokenWrapper.OpeningString[0]
                    },
                (char escapeChar, IEnumerable<char> excludedChars) =>
                    from literal in LiteralString(escapeChar, excludedChars, isWhitespaceAllowed: true, treatWhitespaceAsLiteral: true)
                        .Many()
                        .Flatten()
                    select new Token[] { new LiteralToken(TokenHelper.CollapseStringTokens(literal)) },
                escapeChar,
                Enumerable.Empty<char>())
                .Single()
                .Cast<Token, LiteralToken>();

        /// <summary>
        /// Parses an identifier string wrapped in quotes.
        /// </summary>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="firstCharacterParser">Parser of the first character of the identifier.</param>
        /// <param name="tailCharacterParser">Parser of the rest of the characters of the identifier.</param>
        /// <returns>Parser for an identifier string wrapped in quotes.</returns>
        private static Parser<IEnumerable<Token>> WrappedInQuotesIdentifier(char escapeChar, Parser<char> firstCharacterParser, Parser<char> tailCharacterParser) =>
            IdentifierString(
                escapeChar,
                ExceptQuotes(firstCharacterParser),
                ExceptQuotes(tailCharacterParser));

        /// <summary>
        /// Parses an identifier string.
        /// </summary>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="firstCharacterParser">Parser of the first character of the identifier.</param>
        /// <param name="tailCharacterParser">Parser of the rest of the characters of the identifier.</param>
        /// <returns>Parser for an identifier string.</returns>
        private static Parser<IEnumerable<Token>> IdentifierString(char escapeChar, Parser<char> firstCharacterParser, Parser<char> tailCharacterParser) =>
            from first in ToStringTokens(firstCharacterParser)
            from rest in OrConcat(
                CharWithOptionalLineContinuation(escapeChar, tailCharacterParser),
                EscapedChar(escapeChar))
                .Many()
                .Flatten()
            select TokenHelper.CollapseStringTokens(ConcatTokens(first, rest));

        /// <summary>
        /// Transforms a character parser into a parser for a set of tokens containing a single string token.
        /// </summary>
        /// <param name="parser">Character parser.</param>
        private static Parser<IEnumerable<Token>> ToStringTokens(Parser<char> parser) =>
            from ch in parser
            select new Token[] { new StringToken(ch.ToString()) };

        /// <summary>
        /// Parses a literal string wrapped in quotes.
        /// </summary>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="excludedChars">Characters to exclude from the parsing.</param>
        /// <param name="isWhitespaceAllowed">A value indicating whether whitespace is allowed in the string.</param>
        /// <returns>Parser for a literal string wrapped in quotes.</returns>
        private static Parser<IEnumerable<Token>> WrappedInQuotesLiteralString(char escapeChar, IEnumerable<char> excludedChars, bool isWhitespaceAllowed)
        {
            Parser<char> parser = ExceptQuotes(LiteralChar(escapeChar, excludedChars, isWhitespaceAllowed));
            return
                from first in ToStringTokens(parser).Or(EscapedChar(escapeChar))
                from rest in OrConcat(
                    CharWithOptionalLineContinuation(escapeChar, parser)
                        .Many()
                        .Flatten(),
                    EscapedChar(escapeChar))
                select TokenHelper.CollapseStringTokens(ConcatTokens(first, rest));
        }
            
        /// <summary>
        /// Parses a literal string that is not wrapped in quotes.
        /// </summary>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="excludedChars">Characters to exclude from the parsing.</param>
        /// <param name="isWhitespaceAllowed">A value indicating whether whitespace is allowed in the string.</param>
        /// <returns>Parser for a literal string that is not wrapped in quotes.</returns>
        private static Parser<IEnumerable<Token>> LiteralString(char escapeChar, IEnumerable<char> excludedChars, bool isWhitespaceAllowed,
            bool treatWhitespaceAsLiteral)
        {
            return OrConcat(
                treatWhitespaceAsLiteral ?
                    LiteralStringWithWhitespaceTreatedAsLiteral(escapeChar, excludedChars) :
                    isWhitespaceAllowed ? LiteralStringWithSpaces(escapeChar, excludedChars) : LiteralStringWithoutSpaces(escapeChar, excludedChars),
                EscapedChar(escapeChar));
        }

        /// <summary>
        /// Parses a literal string that treats whitespace as part of the literal.
        /// </summary>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="excludedChars">Characters to exclude from the parsing.</param>
        private static Parser<IEnumerable<Token>> LiteralStringWithWhitespaceTreatedAsLiteral(char escapeChar, IEnumerable<char> excludedChars) =>
            from tokens in CharWithOptionalLineContinuation(escapeChar, Parse.AnyChar.ExceptChars(excludedChars.Append(escapeChar)))
                .Many()
                .Flatten()
            select TokenHelper.CollapseStringTokens(tokens);

        /// <summary>
        /// Parses a literal string that does not contain any spaces.
        /// </summary>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="excludedChars">Characters to exclude from the parsing.</param>
        /// <returns>Parser for a literal string that does not contain any spaces.</returns>
        private static Parser<IEnumerable<Token>> LiteralStringWithoutSpaces(char escapeChar, IEnumerable<char> excludedChars)
        {
            Parser<char> parser = LiteralChar(escapeChar, excludedChars);
            return
                from first in ToStringTokens(parser).Or(EscapedChar(escapeChar))
                from rest in CharWithOptionalLineContinuation(escapeChar, parser)
                    .Many()
                    .Flatten()
                select TokenHelper.CollapseStringTokens(ConcatTokens(first, rest));
        }
            
        /// <summary>
        /// Parses a literal string that contains spaces.
        /// </summary>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="excludedChars">Characters to exclude from the parsing.</param>
        /// <returns>Parser for a literal string that contains spaces.</returns>
        private static Parser<IEnumerable<Token>> LiteralStringWithSpaces(char escapeChar, IEnumerable<char> excludedChars)
        {
            Parser<char> parser = LiteralChar(escapeChar, excludedChars);
            return
                from first in ToStringTokens(parser)
                from rest in (
                    from charsPrecedingWhitespace in CharWithOptionalLineContinuation(escapeChar, parser)
                        .Many()
                        .Flatten()
                    from whitespace in Parse.WhiteSpace.Many().Text()
                    from charsAfterWhitespace in CharWithOptionalLineContinuation(escapeChar, parser)
                       .Many()
                       .Flatten()
                       .Or(EscapedChar(escapeChar))
                       .Many()
                       .Flatten()
                    where charsAfterWhitespace.Any()
                    select ConcatTokens(
                        charsPrecedingWhitespace,
                        new Token?[] { whitespace.Length > 0 ? new WhitespaceToken(whitespace) : null },
                        charsAfterWhitespace))
                    .Many()
                    .Flatten()
                select TokenHelper.CollapseStringTokens(first.Concat(rest));
        }

        /// <summary>
        /// Parses a variable identifier reference.
        /// </summary>
        /// <returns>Parser for a variable identifier.</returns>
        private static Parser<string> VariableIdentifier() =>
            Parse.Identifier(ArgRefFirstLetterParser, ArgRefTailParser);

        /// <summary>
        /// Parses a variable reference using the simple variable syntax.
        /// </summary>
        /// <returns>Parsed variable reference token.</returns>
        private static Parser<VariableRefToken> SimpleVariableReference() =>
            from variableChar in Parse.Char('$')
            from variableIdentifier in VariableIdentifier()
            select new VariableRefToken(new Token[] { new StringToken(variableIdentifier) });

        /// <summary>
        /// Parses a variable reference using the braced variable syntax.
        /// </summary>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="createNonQuotedTokenDelegate">Delegate to create a non-quoted token.</param>
        /// <returns>Parsed variable reference token.</returns>
        private static Parser<VariableRefToken> BracedVariableReference(
            char escapeChar, CreateTokenParserDelegate createNonQuotedTokenDelegate) =>
            from start in Parse.String("${")
            from varNameToken in
                from varName in VariableIdentifier()
                select new StringToken(varName)
            from modifierTokens in (
                from modifier in OrConcat(variableSubstitutionModifiers).Once()
                from modifierValueTokens in ValueOrVariableRef(escapeChar, createNonQuotedTokenDelegate, new char[] { '}' })
                    .AtLeastOnce()
                    .Flatten()
                select ConcatTokens(new SymbolToken(String.Concat(modifier)), new VariableModifierValue(modifierValueTokens))
                ).Optional()
            from end in Parse.Char('}')
            select new VariableRefToken(ConcatTokens(new Token[] { (Token)varNameToken }, modifierTokens.GetOrElse(Enumerable.Empty<Token>())))
            {
                WrappedInBraces = true
            };

        /// <summary>
        /// Parses a variable reference.
        /// </summary>
        /// <typeparam name="TPrimitiveToken">Type of the token for the variable.</typeparam>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="createNonQuotedTokenDelegate">Delegate to create a non-quoted token.</param>
        /// <returns>Parsed variable reference token.</returns>
        private static Parser<VariableRefToken> VariableReference(
            char escapeChar, CreateTokenParserDelegate createNonQuotedTokenDelegate) =>
            SimpleVariableReference()
                .Or(BracedVariableReference(escapeChar, createNonQuotedTokenDelegate));

        /// <summary>
        /// Parses an aggregate containing literals. This handles any variable references.
        /// </summary>
        /// <typeparam name="TToken">Type of the aggregate token.</typeparam>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="isWhitespaceAllowed">A value indicating whether whitespace is allowed in the literal.</param>
        /// <param name="createToken">A delegate to create the aggregate token.</param>
        /// <returns>A parsed aggregate token.</returns>
        public static Parser<TToken> LiteralAggregate<TToken>(
            char escapeChar, bool isWhitespaceAllowed, Func<IEnumerable<Token>, TToken> createToken)
            where TToken : LiteralToken =>
            ValueAggregate(escapeChar, createToken,
                CreateValueTokenDelegate(true,
                    (char escapeChar, IEnumerable<char> excludedChars) =>
                        WrappedInQuotesLiteralString(escapeChar, excludedChars, isWhitespaceAllowed)),
                CreateValueTokenDelegate(false,
                    (char escapeChar, IEnumerable<char> excludedChars) =>
                        LiteralString(escapeChar, excludedChars, isWhitespaceAllowed, treatWhitespaceAsLiteral: false)));

        /// <summary>
        /// Parses an aggregate containing identifiers. This handles any variable references.
        /// </summary>
        /// <typeparam name="TToken">Type of the aggregate token.</typeparam>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="firstCharacterParser">Parser for the first character.</param>
        /// <param name="tailCharacterParser">Parser for the rest of the characters.</param>
        /// <param name="createToken">A delegate to create the aggregate token.</param>
        /// <returns>A parsed aggregate token.</returns>
        public static Parser<TToken> IdentifierAggregate<TToken>(
            Parser<char> firstCharacterParser, Parser<char> tailCharacterParser, char escapeChar,
            Func<IEnumerable<Token>, TToken> createToken)
            where TToken : IdentifierToken =>
            ValueAggregate(escapeChar, createToken,
                CreateValueTokenDelegate(true,
                    (char escapeChar, IEnumerable<char> excludedChars) =>
                        WrappedInQuotesIdentifier(escapeChar, firstCharacterParser, tailCharacterParser)),
                CreateValueTokenDelegate(false,
                    (char escapeChar, IEnumerable<char> excludedChars) =>
                        IdentifierString(escapeChar, firstCharacterParser, tailCharacterParser)));

        /// <summary>
        /// Parses a value aggregate. This handles any variable references.
        /// </summary>
        /// <typeparam name="TAggregateToken">Type of the aggregate token.</typeparam>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="createAggregateToken">A delegate to create the aggregate token.</param>
        /// <param name="createQuotedValueTokenDelegate">A delegate to create a quoted value token.</param>
        /// <param name="createNonQuotedValueTokenDelegate">A delegate to create a non-quoted value token.</param>
        /// <returns></returns>
        private static Parser<TAggregateToken> ValueAggregate<TAggregateToken>(
            char escapeChar, Func<IEnumerable<Token>, TAggregateToken> createAggregateToken,
            CreateTokenParserDelegate createQuotedValueTokenDelegate,
            CreateTokenParserDelegate createNonQuotedValueTokenDelegate)
            where TAggregateToken : AggregateToken, IQuotableToken
        {
            IEnumerable<char> excludedChars = Enumerable.Empty<char>();

            return WrappedInOptionalQuotes(
                (char escapeChar, IEnumerable<char> excludedChars, TokenWrapper tokenWrapper) =>
                    from tokens in ValueOrVariableRef(escapeChar, createQuotedValueTokenDelegate, excludedChars)
                        .Many()
                        .Flatten()
                    select CreateAggregateToken(createAggregateToken, tokens, tokenWrapper),
                (char escapeChar, IEnumerable<char> excludedChars) =>
                    from tokens in ValueOrVariableRef(escapeChar, createNonQuotedValueTokenDelegate, excludedChars)
                        .Many()
                        .Flatten()
                    where tokens.Any()
                    select new Token[] { createAggregateToken(TokenHelper.CollapseStringTokens(tokens)) },
                escapeChar,
                excludedChars)
                .Single()
                .Cast<Token, TAggregateToken>();
        }
        
        /// <summary>
        /// Creates an aggregate token.
        /// </summary>
        /// <typeparam name="TAggregateToken">Type of the aggregate token.</typeparam>
        /// <param name="createToken">A delegate to create the aggregate token.</param>
        /// <param name="childTokens">The child tokens to be contained in the aggregate token.</param>
        /// <param name="tokenWrapper">The token wrapper describing the characters wrapping the token.</param>
        private static TAggregateToken CreateAggregateToken<TAggregateToken>(
            Func<IEnumerable<Token>, TAggregateToken> createToken,
            IEnumerable<Token> childTokens,
            TokenWrapper tokenWrapper)
            where TAggregateToken : AggregateToken, IQuotableToken
        {
            TAggregateToken container = createToken(TokenHelper.CollapseStringTokens(childTokens));
            container.QuoteChar = tokenWrapper.OpeningString[0];
            return container;
        }

        /// <summary>
        /// Returns a delegate that creates a value token parser.
        /// </summary>
        /// <param name="isValueRequired">A value indicating whether a token value is required.</param>
        /// <param name="createValueParser">A delegate to create a parser for the value.</param>
        private static CreateTokenParserDelegate CreateValueTokenDelegate(
            bool isValueRequired, CreateValueParserDelegate createValueParser) =>
            (char escapeChar, IEnumerable<char> excludedChars) =>
                from val in createValueParser(escapeChar, excludedChars)
                where isValueRequired || val.Any()
                select val;

        /// <summary>
        /// Parses a token for either a value or a variable reference.
        /// </summary>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="createParser">A delegate to create the token parser.</param>
        /// <param name="excludedChars">Characters to exclude from the parsed value.</param>
        /// <returns>A token parser.</returns>
        private static Parser<IEnumerable<Token>> ValueOrVariableRef(char escapeChar, CreateTokenParserDelegate createParser,
            IEnumerable<char> excludedChars) =>
            VariableReference(escapeChar, createParser)
                .Select(varRef => new Token[] { varRef })
                .Or(createParser(escapeChar, excludedChars));

        /// <summary>
        /// Parses any character except for whitespace.
        /// </summary>
        public static Parser<char> NonWhitespace() =>
            Parse.AnyChar.Except(Parse.WhiteSpace);

        /// <summary>
        /// Parses a literal character.
        /// </summary>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="excludedChars">Characters to exclude from the parsed value.</param>
        /// <param name="isWhitespaceAllowed">A value indicating whether whitespace is allowed.</param>
        private static Parser<char> LiteralChar(char escapeChar, IEnumerable<char> excludedChars, bool isWhitespaceAllowed = false) =>
            (isWhitespaceAllowed ? Parse.AnyChar : NonWhitespace())
                .ExceptChars(excludedChars)
                .Except(Parse.Char(escapeChar))
                .Except(Parse.Char('$').Then(ch => Parse.LetterOrDigit.Or(Parse.Char('{'))));

        /// <summary>
        /// Parses an escaped character.
        /// </summary>
        /// <param name="escapeChar">Escape character.</param>
        private static Parser<IEnumerable<Token>> EscapedChar(char escapeChar) =>
            from esc in Parse.Char(escapeChar)
            from v in Parse.AnyChar.AsEnumerable()
                .Except(Parse.LineEnd)
                .Text()
            select new Token[] { new StringToken(esc + v) };

        /// <summary>
        /// Parses a token that is optionally wrapped in quotes.
        /// </summary>
        /// <typeparam name="TToken">Type of the token.</typeparam>
        /// <param name="createWrappedParser">A delegate to create a token parser for a wrapped value.</param>
        /// <param name="nonWrappedParser">A delegate to create a token parser for a value that isn't wrapped.</param>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="excludedChars">Characters to exclude from the parsed value.</param>
        private static Parser<IEnumerable<Token>> WrappedInOptionalQuotes<TToken>(CreateWrappedTokenParserDelegate<TToken> createWrappedParser,
            CreateTokenParserDelegate nonWrappedParser, char escapeChar, IEnumerable<char> excludedChars)
            where TToken : Token =>
            WrappedInOptionalCharacters(
                createWrappedParser,
                nonWrappedParser,
                escapeChar,
                excludedChars,
                new TokenWrapper(SingleQuote.ToString(), SingleQuote.ToString()),
                new TokenWrapper(DoubleQuote.ToString(), DoubleQuote.ToString()));

        /// <summary>
        /// Parses a token that is optionally wrapped in a set of characters.
        /// </summary>
        /// <typeparam name="TToken">Type of the token.</typeparam>
        /// <param name="createWrappedParser">A delegate to create a token parser for a wrapped value.</param>
        /// <param name="nonWrappedParser">A delegate to create a token parser for a value that isn't wrapped.</param>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="excludedChars">Characters to exclude from the parsed value.</param>
        /// <param name="tokenWrappers">Set of token wrappers describing the characters that can optionally wrap the value.</param>
        /// <returns></returns>
        private static Parser<IEnumerable<Token>> WrappedInOptionalCharacters<TToken>(CreateWrappedTokenParserDelegate<TToken> createWrappedParser,
            CreateTokenParserDelegate createNonWrappedParser, char escapeChar, IEnumerable<char> excludedChars,
            params TokenWrapper[] tokenWrappers)
            where TToken : Token =>
            tokenWrappers
                .Select(tokenWrapper =>
                    WrappedInCharacters(
                        createWrappedParser,
                        escapeChar,
                        tokenWrapper,
                        excludedChars))
                .Aggregate((current, next) => current.Or(next))
                .Select(token => new Token[] { token })
            .XOr(createNonWrappedParser(escapeChar, excludedChars));

        /// <summary>
        /// Parses a character that excludes quotes.
        /// </summary>
        /// <param name="parser">A character parser to exclude quotes from.</param>
        private static Parser<char> ExceptQuotes(Parser<char> parser) =>
            parser.ExceptChars(Quotes);

        /// <summary>
        /// Parses a token that is wrapped by a set of characters.
        /// </summary>
        /// <typeparam name="TToken">Type of the token.</typeparam>
        /// <param name="createParser">A delegate that creates a token parser.</param>
        /// <param name="escapeChar">Escape character.</param>
        /// <param name="tokenWrapper">A token wrapper describing the set of characters wrapping the value.</param>
        /// <param name="excludedChars">Characters to exclude from the parsed value.</param>
        private static Parser<TToken> WrappedInCharacters<TToken>(CreateWrappedTokenParserDelegate<TToken> createParser,
            char escapeChar, TokenWrapper tokenWrapper, IEnumerable<char> excludedChars)
            where TToken : Token =>
            from opening in Parse.String(tokenWrapper.OpeningString).AsEnumerable()
            from val in createParser(escapeChar, excludedChars, tokenWrapper)
            from closing in Parse.String(tokenWrapper.ClosingString).AsEnumerable()
            select val;

        /// <summary>
        /// Parses an instruction with any trailing content.
        /// </summary>
        /// <param name="instructionName">Name of the instruction.</param>
        /// <param name="escapeChar">Escape character.</param>
        private static Parser<IEnumerable<Token>> InstructionNameWithTrailingContent(string instructionName, char escapeChar) =>
            WithTrailingComments(
                from leading in Whitespace()
                from instruction in TokenWithTrailingWhitespace(InstructionIdentifier(instructionName, escapeChar))
                from lineContinuation in LineContinuation(escapeChar).Optional()
                select ConcatTokens(leading, instruction, new Token[] { lineContinuation.GetOrDefault() }));

        /// <summary>
        /// Parses a set of tokens and any trailing comments.
        /// </summary>
        /// <param name="parser">Set of token parsers.</param>
        private static Parser<IEnumerable<Token>> WithTrailingComments(Parser<IEnumerable<Token?>> parser) =>
            from tokens in parser
            from commentSets in CommentText().Many()
            select ConcatTokens(tokens, commentSets.SelectMany(comments => comments));

        /// <summary>
        /// Delegate for creating a parser of a token that is wrapped by a set of characters.
        /// </summary>
        /// <typeparam name="TToken">Type of the token.</typeparam>
        /// <param name="escapeChar">The escape character.</param>
        /// <param name="excludedChars">Characters to be excluded from parsing.</param>
        /// <param name="tokenWrapper">Description of characters are wrapping the token.</param>
        /// <returns>The token parser.</returns>
        private delegate Parser<TToken> CreateWrappedTokenParserDelegate<TToken>(
            char escapeChar,
            IEnumerable<char> excludedChars,
            TokenWrapper tokenWrapper)
            where TToken : Token;

        /// <summary>
        /// Delegate for creating a parser of a token.
        /// </summary>
        /// <param name="escapeChar">The escape character.</param>
        /// <param name="excludedChars">Characters to be excluded from parsing.</param>
        /// <returns>The token parser.</returns>
        private delegate Parser<IEnumerable<Token>> CreateTokenParserDelegate(
            char escapeChar, IEnumerable<char> excludedChars);

        /// <summary>
        /// Delegate for creating a parser of a primitive string.
        /// </summary>
        ///  <param name="escapeChar">The escape character.</param>
        /// <param name="excludedChars">Characters to be excluded from parsing.</param>
        private delegate Parser<IEnumerable<Token>> CreateValueParserDelegate(char escapeChar, IEnumerable<char> excludedChars);

        /// <summary>
        /// Describes the opening and closing strings that wrap a token.
        /// </summary>
        private class TokenWrapper
        {
            public TokenWrapper(string openingString, string closingString)
            {
                this.OpeningString = openingString;
                this.ClosingString = closingString;
            }

            public string OpeningString { get; }
            public string ClosingString { get; }
        }
    }
}
