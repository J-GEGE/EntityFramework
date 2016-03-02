// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.TestUtilities.FakeProvider;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Storage
{
    public class RawSqlCommandBuilderTest
    {
        [Fact]
        public virtual void Builds_RelationalCommand_without_optional_parameters()
        {
            var builder = new RawSqlCommandBuilder(
                new RelationalCommandBuilderFactory(
                    new FakeSensitiveDataLogger<RelationalCommandBuilderFactory>(),
                    new DiagnosticListener("Fake"),
                    new FakeRelationalTypeMapper()),
                new RelationalSqlGenerationHelper(),
                new ParameterNameGeneratorFactory());

            var command = builder.Build("SQL COMMAND TEXT");

            Assert.Equal("SQL COMMAND TEXT", command.CommandText);
            Assert.Equal(0, command.Parameters.Count);
        }

        [Fact]
        public virtual void Builds_RelationalCommand_with_empty_parameter_list()
        {
            var builder = new RawSqlCommandBuilder(
                new RelationalCommandBuilderFactory(
                    new FakeSensitiveDataLogger<RelationalCommandBuilderFactory>(),
                    new DiagnosticListener("Fake"),
                    new FakeRelationalTypeMapper()),
                new RelationalSqlGenerationHelper(),
                new ParameterNameGeneratorFactory());

            var command = builder.Build("SQL COMMAND TEXT", new object[0]);

            Assert.Equal("SQL COMMAND TEXT", command.Item1.CommandText);
            Assert.Equal(0, command.Item1.Parameters.Count);
            Assert.Equal(0, command.Item2.Count);
        }

        [Fact]
        public virtual void Builds_RelationalCommand_with_parameters()
        {
            var builder = new RawSqlCommandBuilder(
                new RelationalCommandBuilderFactory(
                    new FakeSensitiveDataLogger<RelationalCommandBuilderFactory>(),
                    new DiagnosticListener("Fake"),
                    new FakeRelationalTypeMapper()),
                new RelationalSqlGenerationHelper(),
                new ParameterNameGeneratorFactory());

            var command = builder.Build("SQL COMMAND TEXT {0} {1} {2}", new object[] { 1, 2L, "three" });

            Assert.Equal("SQL COMMAND TEXT @p0 @p1 @p2", command.Item1.CommandText);
            Assert.Equal(3, command.Item1.Parameters.Count);
            Assert.Equal("p0", command.Item1.Parameters[0].InvariantName);
            Assert.Equal("p1", command.Item1.Parameters[1].InvariantName);
            Assert.Equal("p2", command.Item1.Parameters[2].InvariantName);

            Assert.Equal(3, command.Item2.Count);
            Assert.Equal(1, command.Item2["p0"]);
            Assert.Equal(2L, command.Item2["p1"]);
            Assert.Equal("three", command.Item2["p2"]);
        }
    }
}
