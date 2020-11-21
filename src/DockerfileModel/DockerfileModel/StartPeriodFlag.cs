﻿using System.Collections.Generic;
using DockerfileModel.Tokens;
using Sprache;
using static DockerfileModel.ParseHelper;

namespace DockerfileModel
{
    public class StartPeriodFlag : KeyValueToken<KeywordToken, LiteralToken>
    {
        internal StartPeriodFlag(IEnumerable<Token> tokens) : base(tokens)
        {
        }

        public static StartPeriodFlag Create(string startPeriod) =>
            Create(new KeywordToken("start-period"), new LiteralToken(startPeriod), tokens => new StartPeriodFlag(tokens), isFlag: true);

        public static StartPeriodFlag Parse(string text,
            char escapeChar = Dockerfile.DefaultEscapeChar) =>
            Parse(text, Keyword("start-period", escapeChar), LiteralAggregate(escapeChar), tokens => new StartPeriodFlag(tokens), escapeChar: escapeChar);

        public static Parser<StartPeriodFlag> GetParser(char escapeChar = Dockerfile.DefaultEscapeChar) =>
            GetParser(Keyword("start-period", escapeChar), LiteralAggregate(escapeChar), tokens => new StartPeriodFlag(tokens), escapeChar: escapeChar);
    }
}