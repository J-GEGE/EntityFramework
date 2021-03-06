﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.EntityFrameworkCore.FunctionalTests;
using Microsoft.EntityFrameworkCore.FunctionalTests.TestModels.UpdatesModel;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.EntityFrameworkCore.InMemory.FunctionalTests
{
    public class UpdatesInMemoryFixture : UpdatesFixtureBase<InMemoryTestStore>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly DbContextOptionsBuilder _optionsBuilder;

        public UpdatesInMemoryFixture()
        {
            _serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddInMemoryDatabase()
                .ServiceCollection()
                .BuildServiceProvider();

            _optionsBuilder = new DbContextOptionsBuilder();
            _optionsBuilder.UseInMemoryDatabase();
        }

        public override InMemoryTestStore CreateTestStore()
            => InMemoryTestStore.CreateScratch(
                () =>
                {
                    using (var context = new UpdatesContext(_serviceProvider, _optionsBuilder.Options))
                    {
                        UpdatesModelInitializer.Seed(context);
                    }
                },
                () =>
                {
                    _serviceProvider.GetRequiredService<IInMemoryStore>().Clear();
                });

        public override UpdatesContext CreateContext(InMemoryTestStore testStore)
        {
            return new UpdatesContext(_serviceProvider, _optionsBuilder.Options);
        }
    }
}
