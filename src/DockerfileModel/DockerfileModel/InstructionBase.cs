﻿using System.Collections.Generic;
using System.Linq;
using Sprache;

namespace DockerfileModel
{
    public abstract class InstructionBase : DockerfileLine
    {
        protected InstructionBase(string text, Parser<IEnumerable<Token?>> parser)
            : base(text, parser)
        {
        }

        public string InstructionName
        {
            get => this.InstructionNameToken.Value;
            set { this.InstructionNameToken.Value = value; }
        }

        private KeywordToken InstructionNameToken => Tokens.OfType<KeywordToken>().First();

        public IEnumerable<CommentToken> Comments => GetComments();

        public override LineType Type => LineType.Instruction;

        public void ResolveArgValues(IDictionary<string, string?> argValues, char escapeChar)
        {
            new ArgResolverVisitor(argValues, escapeChar).Visit(this);
        }

        private class ArgResolverVisitor : TokenVisitor
        {
            private readonly IDictionary<string, string?> argValues;
            private readonly char escapeChar;

            public ArgResolverVisitor(IDictionary<string, string?> argValues, char escapeChar)
            {
                this.argValues = argValues;
                this.escapeChar = escapeChar;
            }

            protected override void VisitLiteralToken(LiteralToken token)
            {
                base.VisitLiteralToken(token);
                token.Value = ArgResolver.Resolve(token.Value, argValues, escapeChar);
            }
        }
    }
}