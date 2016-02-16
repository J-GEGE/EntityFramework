// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities.FakeProvider;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Storage
{
    public class RelationalParameterListTest
    {
        [Fact]
        public void Can_add_dynamic_parameter()
        {
            var typeMapper = new FakeRelationalTypeMapper();

            var parameterList = new RelationalParameterList(typeMapper);

            parameterList.AddParameter(
                "InvariantName",
                "Name");

            Assert.Equal(1, parameterList.Parameters.Count);

            var parameter = parameterList.Parameters[0] as DynamicRelationalParameter;

            Assert.NotNull(parameter);
            Assert.Equal("InvariantName", parameter.InvariantName);
            Assert.Equal("Name", parameter.Name);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Can_add_type_mapped_parameter_by_type(bool nullable)
        {
            var typeMapper = new FakeRelationalTypeMapper();

            var type = nullable
                ? typeof(int?)
                : typeof(int);

            var parameterList = new RelationalParameterList(typeMapper);

            parameterList.AddParameter(
                "InvariantName",
                "Name",
                type);

            Assert.Equal(1, parameterList.Parameters.Count);

            var parameter = parameterList.Parameters[0] as TypeMappedRelationalParameter;

            Assert.NotNull(parameter);
            Assert.Equal("InvariantName", parameter.InvariantName);
            Assert.Equal("Name", parameter.Name);
            Assert.Equal(typeMapper.GetMapping(typeof(int)), parameter.RelationalTypeMapping);
            Assert.Equal(nullable, parameter.Nullable);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Can_add_type_mapped_parameter_by_property(bool nullable)
        {
            var typeMapper = new FakeRelationalTypeMapper();

            var property = new Model().AddEntityType("MyType").AddProperty("MyProp", typeof(string));
            property.IsNullable = nullable;

            var parameterList = new RelationalParameterList(typeMapper);

            parameterList.AddParameter(
                "InvariantName",
                "Name",
                property);

            Assert.Equal(1, parameterList.Parameters.Count);

            var parameter = parameterList.Parameters[0] as TypeMappedRelationalParameter;

            Assert.NotNull(parameter);
            Assert.Equal("InvariantName", parameter.InvariantName);
            Assert.Equal("Name", parameter.Name);
            Assert.Equal(typeMapper.GetMapping(property), parameter.RelationalTypeMapping);
            Assert.Equal(nullable, parameter.Nullable);
        }

        [Fact]
        public void Can_add_composite_parameter()
        {
            var typeMapper = new FakeRelationalTypeMapper();

            var parameterList = new RelationalParameterList(typeMapper);

            parameterList.AddCompositeParameter(
                "CompositeInvariant",
                list =>
                {
                    list.AddParameter(
                        "FirstInvariant",
                        "FirstName",
                        typeof(int));

                    list.AddParameter(
                        "SecondInvariant",
                        "SecondName",
                        typeof(string));
                });

            Assert.Equal(1, parameterList.Parameters.Count);

            var parameter = parameterList.Parameters[0] as CompositeRelationalParameter;

            Assert.NotNull(parameter);
            Assert.Equal("CompositeInvariant", parameter.InvariantName);
            Assert.Equal(2, parameter.RelationalParameters.Count);
        }

        [Fact]
        public void Does_not_add_empty_composite_parameter()
        {
            var typeMapper = new FakeRelationalTypeMapper();

            var parameterList = new RelationalParameterList(typeMapper);

            parameterList.AddCompositeParameter(
                "CompositeInvariant",
                list => { });

            Assert.Equal(0, parameterList.Parameters.Count);
        }
    }
}
