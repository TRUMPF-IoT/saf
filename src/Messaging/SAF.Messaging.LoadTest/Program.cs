// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Common;
using SAF.Hosting;
using System.Diagnostics;
using SAF.Messaging.Redis;
using SAF.Messaging.InProcess;

namespace SAF.Messaging.LoadTest;

class Program
{
    static void Main(string[] args)
    {
        var sc = new ServiceCollection();

        sc.AddLogging(l => l.AddConsole());
        sc.AddSingleton<IServiceMessageDispatcher, ServiceMessageDispatcher>();

        var n = 500;
        var msWait = 100;

        //TestRedis(sc, n, msWait);
        TestInProcess(sc, n, msWait);

        Console.ReadLine();
    }

    static void TestRedis(IServiceCollection sc, int n, int msWait)
    {
        sc.AddRedisInfrastructure(c => c.ConnectionString = "localhost");
        using (var sp = sc.BuildServiceProvider())
        {
            RunMessagingLoadTest(sp, n, msWait);
            //RunMessagingLoadTestAsync(sp, n, msWait).Wait();
        }
    }

    static void TestInProcess(IServiceCollection sc, int n, int msWait)
    {
        sc.AddInProcessMessagingInfrastructure()
            .AddSingleton<IMessagingInfrastructure>(sp => sp.GetRequiredService<IInProcessMessagingInfrastructure>());
        using (var sp = sc.BuildServiceProvider())
        {
            RunMessagingLoadTest(sp, n, msWait);
            //RunMessagingLoadTestAsync(sp, n, msWait).Wait();
        }
    }

    static void RunMessagingLoadTest(IServiceProvider sp, int n, int msWait)
    {
        var messaging = sp.GetRequiredService<IMessagingInfrastructure>();
        var log = sp.GetService<ILogger<Program>>();

        var sync = new object();
        var open = 0;

        // REDIS
        // Parallel creation (MaxDegreeOfParallelism unlimited) used around 99 threads and > 13 seconds.
        // Limited Parallel creation (MaxDegreeOfParallelism = 20) used 500ms.
        // Limited Parallel creation (MaxDegreeOfParallelism = 8) used 256ms.
        // Serial creation (MaxDegreeOfParallelism = 1) used 386ms.

        // ===> CONCLUSION 1: SERIALIZE OR LIMIT PARALLELITY OF SUBSCRIPTION CREATION!
        // SEEMS LIKE THREAD CREATION ISN'T THE BIG PROBLEM HERE, MAYBE LOCKING OVERHEAD ON THE REDIS CONNECTION ITSELF.

        // For Redis, async subscription creation and publishing would be much better. 
        // But changing these interfaces would have a big impact on all messaging implementations. 
        // We leave it like this for the moment.

        var sw = new Stopwatch();
        sw.Start();

        Parallel.For(0, n, i => 
        {
            messaging.Subscribe($"load/test/{i}", m =>
            {
                Thread.Sleep(msWait);
                lock (sync)
                    open--;
            });
        });

        log?.LogInformation("[SYNC] Subscriptions used {0}ms.", sw.ElapsedMilliseconds);
        sw.Restart();

        Parallel.For(0, n, i =>
        {
            lock (sync)
                open++;

            messaging.Publish(new Message { Topic = $"load/test/{i}", Payload = "something" });
        });
        log?.LogInformation("[SYNC] Publishes used {0}ms.", sw.ElapsedMilliseconds);
        sw.Restart();

        while (open > 0)
        {
            Thread.Yield();
        }

        log?.LogInformation("[SYNC] Handlers used {0}ms.", sw.ElapsedMilliseconds);
    }
}