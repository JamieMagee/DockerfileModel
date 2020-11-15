﻿using System;
using System.Collections.Generic;
using System.Linq;
using DockerfileModel.Tokens;
using Sprache;
using Xunit;

using static DockerfileModel.Tests.TokenValidator;

namespace DockerfileModel.Tests
{
    public class ChangeOwnerTests
    {
        [Theory]
        [MemberData(nameof(ParseTestInput))]
        public void Parse(ChangeOwnerParseTestScenario scenario)
        {
            if (scenario.ParseExceptionPosition is null)
            {
                ChangeOwner result = ChangeOwner.Parse(scenario.Text, scenario.EscapeChar);
                Assert.Equal(scenario.Text, result.ToString());
                Assert.Collection(result.Tokens, scenario.TokenValidators);
                scenario.Validate?.Invoke(result);
            }
            else
            {
                ParseException exception = Assert.Throws<ParseException>(
                    () => ArgInstruction.Parse(scenario.Text, scenario.EscapeChar));
                Assert.Equal(scenario.ParseExceptionPosition.Line, exception.Position.Line);
                Assert.Equal(scenario.ParseExceptionPosition.Column, exception.Position.Column);
            }
        }

        [Theory]
        [MemberData(nameof(CreateTestInput))]
        public void Create(CreateTestScenario scenario)
        {
            ChangeOwner result = ChangeOwner.Create(scenario.User, scenario.Group);
            Assert.Collection(result.Tokens, scenario.TokenValidators);
            scenario.Validate?.Invoke(result);
        }

        [Fact]
        public void User()
        {
            ChangeOwner changeOwner = ChangeOwner.Create("test", "group");
            Assert.Equal("test", changeOwner.User);
            Assert.Equal("test", changeOwner.UserToken.Value);

            changeOwner.User = "test2";
            Assert.Equal("test2", changeOwner.User);
            Assert.Equal("test2", changeOwner.UserToken.Value);

            changeOwner.UserToken.Value = "test3";
            Assert.Equal("test3", changeOwner.User);
            Assert.Equal("test3", changeOwner.UserToken.Value);

            changeOwner.UserToken = new LiteralToken("test4");
            Assert.Equal("test4", changeOwner.User);
            Assert.Equal("test4", changeOwner.UserToken.Value);
            Assert.Equal("test4:group", changeOwner.ToString());

            Assert.Throws<ArgumentException>(() => changeOwner.User = "");
            Assert.Throws<ArgumentNullException>(() => changeOwner.User = null);
            Assert.Throws<ArgumentNullException>(() => changeOwner.UserToken = null);
        }

        [Fact]
        public void Group()
        {
            ChangeOwner changeOwner = ChangeOwner.Create("user", "test");
            Assert.Equal("test", changeOwner.Group);
            Assert.Equal("test", changeOwner.GroupToken.Value);

            changeOwner.Group = "test2";
            Assert.Equal("test2", changeOwner.Group);
            Assert.Equal("test2", changeOwner.GroupToken.Value);

            changeOwner.GroupToken.Value = "test3";
            Assert.Equal("test3", changeOwner.Group);
            Assert.Equal("test3", changeOwner.GroupToken.Value);

            changeOwner.Group = null;
            Assert.Null(changeOwner.Group);
            Assert.Null(changeOwner.GroupToken);
            Assert.Equal("user", changeOwner.ToString());

            changeOwner.GroupToken = new LiteralToken("test4");
            Assert.Equal("test4", changeOwner.Group);
            Assert.Equal("test4", changeOwner.GroupToken.Value);
            Assert.Equal("user:test4", changeOwner.ToString());

            changeOwner.GroupToken = null;
            Assert.Null(changeOwner.Group);
            Assert.Null(changeOwner.GroupToken);
            Assert.Equal("user", changeOwner.ToString());

            changeOwner.Group = "";
            Assert.Null(changeOwner.Group);
            Assert.Null(changeOwner.GroupToken);
            Assert.Equal("user", changeOwner.ToString());
        }

        public static IEnumerable<object[]> ParseTestInput()
        {
            ChangeOwnerParseTestScenario[] testInputs = new ChangeOwnerParseTestScenario[]
            {
                new ChangeOwnerParseTestScenario
                {
                    Text = "55:mygroup",
                    TokenValidators = new Action<Token>[]
                    {
                        token => ValidateLiteral(token, "55"),
                        token => ValidateSymbol(token, ':'),
                        token => ValidateLiteral(token, "mygroup")
                    },
                    Validate = result =>
                    {
                        Assert.Equal("55", result.User);
                        Assert.Equal("mygroup", result.Group);
                    }
                },
                new ChangeOwnerParseTestScenario
                {
                    Text = "bin",
                    TokenValidators = new Action<Token>[]
                    {
                        token => ValidateLiteral(token, "bin")
                    },
                    Validate = result =>
                    {
                        Assert.Equal("bin", result.User);
                        Assert.Null(result.Group);
                    }
                },
                new ChangeOwnerParseTestScenario
                {
                    EscapeChar = '`',
                    Text = "us`\ner`\n:`\ngr`\noup",
                    TokenValidators = new Action<Token>[]
                    {
                        token => ValidateAggregate<LiteralToken>(token, "us`\ner",
                            token => ValidateString(token, "us"),
                            token => ValidateLineContinuation(token, '`', "\n"),
                            token => ValidateString(token, "er")),
                        token => ValidateLineContinuation(token, '`', "\n"),
                        token => ValidateSymbol(token, ':'),
                        token => ValidateLineContinuation(token, '`', "\n"),
                        token => ValidateAggregate<LiteralToken>(token, "gr`\noup",
                            token => ValidateString(token, "gr"),
                            token => ValidateLineContinuation(token, '`', "\n"),
                            token => ValidateString(token, "oup"))
                    },
                    Validate = result =>
                    {
                        Assert.Equal("user", result.User);
                        Assert.Equal("group", result.Group);

                        result.Group = null;
                        Assert.Equal("us`\ner`\n", result.ToString());
                    }
                },
                new ChangeOwnerParseTestScenario
                {
                    Text = "$user:group$var",
                    TokenValidators = new Action<Token>[]
                    {
                        token => ValidateAggregate<LiteralToken>(token, "$user",
                            token => ValidateAggregate<VariableRefToken>(token, "$user",
                                token => ValidateString(token, "user"))),
                        token => ValidateSymbol(token, ':'),
                        token => ValidateAggregate<LiteralToken>(token, "group$var",
                            token => ValidateString(token, "group"),
                            token => ValidateAggregate<VariableRefToken>(token, "$var",
                                token => ValidateString(token, "var")))
                    }
                },
                new ChangeOwnerParseTestScenario
                {
                    Text = "user:",
                    ParseExceptionPosition = new Position(1, 1, 1)
                },
                new ChangeOwnerParseTestScenario
                {
                    Text = ":group",
                    ParseExceptionPosition = new Position(1, 1, 1)
                }
            };

            return testInputs.Select(input => new object[] { input });
        }

        public static IEnumerable<object[]> CreateTestInput()
        {
            CreateTestScenario[] testInputs = new CreateTestScenario[]
            {
                new CreateTestScenario
                {
                    User = "user",
                    Group = "group",
                    TokenValidators = new Action<Token>[]
                    {
                        token => ValidateLiteral(token, "user"),
                        token => ValidateSymbol(token, ':'),
                        token => ValidateLiteral(token, "group")
                    },
                    Validate = result =>
                    {
                        Assert.Equal("user", result.User);
                        Assert.Equal("group", result.Group);
                    }
                },
                new CreateTestScenario
                {
                    User = "user",
                    Group = null,
                    TokenValidators = new Action<Token>[]
                    {
                        token => ValidateLiteral(token, "user")
                    },
                    Validate = result =>
                    {
                        Assert.Equal("user", result.User);
                        Assert.Null(result.Group);
                    }
                }
            };

            return testInputs.Select(input => new object[] { input });
        }

        public class ChangeOwnerParseTestScenario : ParseTestScenario<ChangeOwner>
        {
            public char EscapeChar { get; set; }
        }

        public class CreateTestScenario : TestScenario<ChangeOwner>
        {
            public string User { get; set; }
            public string Group { get; set; }
        }
    }
}