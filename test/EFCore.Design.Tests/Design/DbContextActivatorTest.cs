// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.EntityFrameworkCore.Design
{
    public class DbContextActivatorTest
    {
        [ConditionalFact]
        public void CreateInstance_works()
        {
            var result = DbContextActivator.CreateInstance(typeof(TestContext));

            Assert.IsType<TestContext>(result);
        }

        [ConditionalFact]
        public void CreateInstance_can_pass_in_arguments()
        {
            var result = DbContextActivator.CreateInstance(
                typeof(ArgsContext), arguments: new string[] { "Pass", "Me", "In" });

            Assert.IsType<ArgsContext>(result);
            Assert.Collection<string>(((ArgsContext)result).Args,
                 s => { Assert.Equal("Pass", s); },
                 s => { Assert.Equal("Me", s); },
                 s => { Assert.Equal("In", s); });
        }

        private class TestContext : DbContext
        {
            protected override void OnConfiguring(DbContextOptionsBuilder options)
                => options
                    .EnableServiceProviderCaching(false)
                    .UseInMemoryDatabase(nameof(DbContextActivatorTest));
        }

        private class ArgsContext : DbContext
        {
            public string[] Args { get; set; }

            public ArgsContext(string[] args)
            {
                Args = args;
            }

            protected override void OnConfiguring(DbContextOptionsBuilder options)
                => options
                    .EnableServiceProviderCaching(false)
                    .UseInMemoryDatabase(nameof(DbContextActivatorTest));
        }
    }
}
