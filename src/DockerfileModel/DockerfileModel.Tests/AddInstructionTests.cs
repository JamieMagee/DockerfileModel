﻿using System.Collections.Generic;
using Xunit;

namespace DockerfileModel.Tests
{
    public class AddInstructionTests : FileTransferInstructionTests<AddInstruction>
    {
        public AddInstructionTests()
            : base(AddInstruction.Parse, AddInstruction.Create)
        {
        }

        [Theory]
        [MemberData(nameof(ParseTestInput))]
        public void Parse(FileTransferInstructionParseTestScenario scenario) => RunParseTest(scenario);

        [Theory]
        [MemberData(nameof(CreateTestInput))]
        public void Create(CreateTestScenario scenario) => RunCreateTest(scenario);

        public static IEnumerable<object[]> ParseTestInput() => ParseTestInput("ADD");


        public static IEnumerable<object[]> CreateTestInput() => CreateTestInput("ADD");
    }
}