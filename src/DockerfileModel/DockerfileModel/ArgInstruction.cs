﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sprache;

using static DockerfileModel.ParseHelper;

namespace DockerfileModel
{
    public class ArgInstruction : InstructionBase
    {
        private ArgInstruction(string text, char escapeChar)
            : base(text, GetParser(escapeChar))
        {
        }

        public string ArgName
        {
            get => this.Tokens.OfType<IdentifierToken>().First().Value;
            set { this.Tokens.OfType<IdentifierToken>().First().Value = value; }
        }

        public string? ArgValue
        {
            get => this.Tokens.OfType<LiteralToken>().FirstOrDefault()?.Value;
            set
            {
                LiteralToken argValue = this.Tokens.OfType<LiteralToken>().FirstOrDefault();
                if (argValue != null)
                {
                    if (value is null)
                    {
                        this.TokenList.RemoveRange(this.TokenList.IndexOf(argValue) - 1, 2);
                    }
                    else
                    {
                        argValue.Value = value;
                    }
                }
                else if (value != null)
                {
                    this.TokenList.AddRange(new Token[]
                    {
                        new PunctuationToken("="),
                        new LiteralToken(value)
                    });
                }
            }
        }

        public static ArgInstruction Parse(string text, char escapeChar) =>
            new ArgInstruction(text, escapeChar);

        public static ArgInstruction Create(string argName, string? argValue = null)
        {
            StringBuilder builder = new StringBuilder($"ARG {argName}");
            if (argValue != null)
            {
                builder.Append($"={argValue}");
            }

            return Parse(builder.ToString(), Instruction.DefaultEscapeChar);
        }

        public static Parser<IEnumerable<Token>> GetParser(char escapeChar) =>
            Instruction("ARG", escapeChar, GetArgsParser(escapeChar));

        private static Parser<IEnumerable<Token>> GetArgsParser(char escapeChar) =>
            ArgTokens(
                from argName in GetArgNameParser(escapeChar).AsEnumerable()
                from argAssignment in GetArgAssignmentParser(escapeChar).Optional()
                select ConcatTokens(
                    argName,
                    argAssignment.GetOrDefault()), escapeChar).End();

        private static Parser<IdentifierToken> GetArgNameParser(char escapeChar) =>
            QuotableIdentifier(Sprache.Parse.Letter, Sprache.Parse.LetterOrDigit.Or(Sprache.Parse.Char('_')), escapeChar);

        private static Parser<IEnumerable<Token>> GetArgAssignmentParser(char escapeChar) =>
            from assignmentChar in Sprache.Parse.Char('=')
            from value in Literal(escapeChar).Optional()
            select ConcatTokens(
                new PunctuationToken(assignmentChar.ToString()),
                value.GetOrElse(new LiteralToken("")));
    }
}
