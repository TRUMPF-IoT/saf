// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;

namespace TestSequenceRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            using(var runner = new SAF.DevToolbox.TestRunner.TestSequenceRunner())
            {
                // runner.UseRedisInfrastructure()
                // runner.UseCdeInfrastructure()
                runner.UseInProcessInfrastructure()
                    .TraceTestSequences()
                    .AddTestSequence<TestSequence>()
                    .Run();
            }

            Console.ReadLine();
        }
    }
}
