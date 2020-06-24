using System;
using System.IO;
using MongoDB.Bson;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson.IO;

namespace WorkloadExecutor
{
    class Program
    {
        static long __numberOfSuccessfulOperations;
        static long __numberOfFailedOperations;
        static long __numberOfOperationErrors;

        static System.Diagnostics.Stopwatch __stopwatch = new System.Diagnostics.Stopwatch();
        static async Task Main(string[] args)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (o, e) => HandleCancel(e, cancellationTokenSource);

            var resultsDir = Environment.GetEnvironmentVariable("RESULTS_DIR");
            var resultsPath = resultsDir == null ? "results.json" : Path.Combine(resultsDir, "results.json");
            Console.WriteLine($"dotnet main> Results will be written to {resultsPath}..." );

            try
            {
                // register all interrupt and exit handlers
                Console.CancelKeyPress += cancelHandler;
                foreach (var arg in args)
                {
                    Console.WriteLine($"dotnet main> args: {arg}");
                }

                var connectionString = args[0];
                var driverWorkload = BsonDocument.Parse(args[1]);
                Console.WriteLine("dotnet main> Starting workload executor...");

                if (!bool.TryParse(Environment.GetEnvironmentVariable("ASYNC"), out bool async))
                {
                    async = true;
                }
                if (async)
                {
                    await ExecuteWorkloadAsync(connectionString, driverWorkload, cancellationTokenSource.Token).ConfigureAwait(false);
                }
                else
                {
                    ExecuteWorkload(connectionString, driverWorkload, cancellationTokenSource.Token);
                }


            }
            finally // HandleCancel and this finally clause must finish executing well under 50ms if we want them to finish
            {
                Console.WriteLine("dotnet main finally>");
                // ensure we don't catch multiple cancels
                Console.CancelKeyPress -= cancelHandler;
                __stopwatch.Start();
                Console.WriteLine("dotnet main finally> Writing final results file");
                var resultsJson = ConvertResultsToJson();
                Console.WriteLine(resultsJson);
#if NETCOREAPP2_1
                await File.WriteAllTextAsync(resultsPath, resultsJson, CancellationToken.None).ConfigureAwait(false);
#else
                File.WriteAllText(resultsPath, resultsJson);
#endif
                Console.WriteLine("dotnet main finally> Wrote results.json");
                Console.WriteLine($"dotnet main finally> Total time (ms) elapsed since sigint: {__stopwatch.ElapsedMilliseconds}");
                __stopwatch.Stop();
            }
        }

        static string ConvertResultsToJson()
        {
                var results = new BsonDocument
                {
                    {"numErrors", Interlocked.Read(ref __numberOfOperationErrors)},
                    {"numFailures", Interlocked.Read(ref __numberOfFailedOperations)},
                    {"numSuccesses", Interlocked.Read(ref __numberOfSuccessfulOperations)}
                };
                var resultsJson = results.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });
                return resultsJson;
        }

        static string QuicklyConvertResultsToJson()
        {
                var resultsJson =
                @"{ " +
                $"  \"numErrors\" : {Interlocked.Read(ref __numberOfOperationErrors)}, " +
                $"  \"numFailures\" : {Interlocked.Read(ref __numberOfFailedOperations)}, " +
                $"  \"numSuccesses\" : {Interlocked.Read(ref __numberOfSuccessfulOperations)}  " +
                @"} ";
                return resultsJson;
        }
        static void ExecuteWorkload(string connectionString, BsonDocument driverWorkload, CancellationToken cancellationToken)
        {
            Environment.SetEnvironmentVariable("MONGODB_URI", connectionString);
            var testRunner = new AstrolabeTestRunner(
                incrementOperationSuccesses: () => Interlocked.Increment(ref __numberOfSuccessfulOperations),
                incrementOperationErrors: () => Interlocked.Increment(ref __numberOfOperationErrors),
                incrementOperationFailures: () => Interlocked.Increment(ref __numberOfFailedOperations),
                cancellationToken: cancellationToken);
            var factory = new AstrolabeTestRunner.TestCaseFactory();
            var testCase = factory.CreateTestCase(driverWorkload);
            testCase.Test["async"] = false;
            while (!cancellationToken.IsCancellationRequested)
            {
                testRunner.Run(testCase);
            }
            Console.WriteLine("dotnet ExecuteWorkload> Returning...");
        }

        static async Task ExecuteWorkloadAsync(string connectionString, BsonDocument driverWorkload, CancellationToken cancellationToken)
        {
            Environment.SetEnvironmentVariable("MONGODB_URI", connectionString);
            var testRunner = new AstrolabeTestRunner(
                incrementOperationSuccesses: () => Interlocked.Increment(ref __numberOfSuccessfulOperations),
                incrementOperationErrors: () => Interlocked.Increment(ref __numberOfOperationErrors),
                incrementOperationFailures: () => Interlocked.Increment(ref __numberOfFailedOperations),
                cancellationToken: cancellationToken);
            var factory = new AstrolabeTestRunner.TestCaseFactory();
            var testCase = factory.CreateTestCase(driverWorkload);
            await testRunner.RunAsync(testCase).ConfigureAwait(false);
            Console.WriteLine("dotnet ExecuteWorkloadAsync> returning...");
        }

        internal static void CancelWorkloadTask(CancellationTokenSource cancellationTokenSource)
        {
            Console.Write($"\ndotnet cancel workload> Canceling the workload task...");
            cancellationTokenSource.Cancel();
            Console.WriteLine($"Done.");
        }

        // This method and main's finally clause must finish well under 50ms due to Cygwin bash incorrectly
        // terminating this process before the handlers can finish executing
        internal static void HandleCancel(
            ConsoleCancelEventArgs args,
            CancellationTokenSource cancellationTokenSource)
        {
            __stopwatch.Start();
            Console.WriteLine($"dotnet int handler> Execution interrupted via {args.GetType()}");
            Console.WriteLine($"dotnet int handler>  Key pressed: {args.SpecialKey}");
            Console.WriteLine($"dotnet int handler>  Cancel property: {args.Cancel}");

            // Per the documentation example: https://docs.microsoft.com/en-us/dotnet/api/system.consolecanceleventargs.cancel
            // we set the Cancel property to true to prevent the process from terminating. This doesn't work properly in Cygwin.
            Console.WriteLine("dotnet int handler> Setting the Cancel property to true to prevent process from terminating...");
            args.Cancel = true;

            Console.WriteLine($"dotnet int handler>   Cancel property: {args.Cancel}");
            Console.WriteLine($"dotnet int handler> Time elapsed: {__stopwatch.ElapsedMilliseconds}");
            CancelWorkloadTask(cancellationTokenSource);
        }
    }
}
