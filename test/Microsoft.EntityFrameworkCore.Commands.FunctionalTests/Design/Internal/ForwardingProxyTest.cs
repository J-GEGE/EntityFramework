// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if (NET451 || DNX451)

using System;
using Microsoft.EntityFrameworkCore.FunctionalTests.TestUtilities.Xunit;
using Microsoft.EntityFrameworkCore.Commands.TestUtilities;
using Microsoft.EntityFrameworkCore.Relational.Design.FunctionalTests.TestUtilities;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Design.Internal
{
    internal interface IMagic
    {
        int Number { get; }
    }

    public class ForwardingProxyTest
    {
        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Remoting on Mono is buggy")]
        public void Forwards_to_instances_of_a_different_type()
        {
            using (var directory = new TempDirectory())
            {
                var source = new BuildSource
                {
                    TargetDir = directory.Path,
                    Sources = { @"
                        using System;
                        namespace Microsoft.EntityFrameworkCore.Design.Internal
                        {
                            // NOTE: This interface will have a different identity than the one above
                            internal interface IMagic
                            {
                                int Number { get; }
                            }
                            internal class Magic : MarshalByRefObject, IMagic
                            {
                                public int Number
                                {
                                    get { return 7; }
                                }
                            }
                        }" }
                };
                var build = source.Build();

                var domain = AppDomain.CreateDomain(
                    "ForwardingProxyTest",
                    null,
                    new AppDomainSetup { ApplicationBase = build.TargetDir });
                try
                {
                    var target = domain.CreateInstanceAndUnwrap(
                        build.TargetName,
                        "Microsoft.EntityFrameworkCore.Design.Internal.Magic");
                    var forwardingProxy = new ForwardingProxy<IMagic>(target);
                    var transparentProxy = forwardingProxy.GetTransparentProxy();

                    Assert.Equal(7, transparentProxy.Number);
                }
                finally
                {
                    AppDomain.Unload(domain);
                }
            }
        }
    }
}

#endif
