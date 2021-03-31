﻿/* Copyright 2020-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AstrolabeWorkloadExecutor;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.TestHelpers.JsonDrivenTests;
using MongoDB.Driver;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Tests.UnifiedTestOperations;

namespace WorkloadExecutor
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Ensure.IsEqualTo(args.Length, 2, nameof(args.Length));

            var connectionString = args[0];
            var driverWorkload = BsonDocument.Parse(args[1]);
            Console.WriteLine($"Income document: {driverWorkload}");

            var cancellationTokenSource = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (o, e) => HandleCancel(e, cancellationTokenSource);

            var resultsDir = Environment.GetEnvironmentVariable("RESULTS_DIR");
            var eventsPath = Path.Combine(resultsDir ?? "", "events.json");
            var resultsPath = Path.Combine(resultsDir ?? "", "results.json");
            Console.WriteLine($"dotnet main> Results will be written to {resultsPath}");
            Console.WriteLine($"dotnet main> Events will be written to {eventsPath}");

            Console.CancelKeyPress += cancelHandler;

            Console.WriteLine("dotnet main> Starting workload executor...");

            if (!bool.TryParse(Environment.GetEnvironmentVariable("ASYNC"), out bool async))
            {
                async = true;
            }

            using (var entityMap = ExecuteWorkload(driverWorkload, async, cancellationTokenSource.Token))
            {
                ExtractResults(entityMap, out var eventsJson, out var resultsJson);

                Console.WriteLine("dotnet main finally> Writing final results and events files");
                File.WriteAllText(resultsPath, resultsJson);
                File.WriteAllText(eventsPath, eventsJson);
            }

            Console.CancelKeyPress -= cancelHandler;

            // ensure all messages are propagated to the astrolabe time immediately
            Console.Error.Flush();
            Console.Out.Flush();
        }

        private static void ExtractResults(UnifiedEntityMap entityMap, out string eventsJson, out string resultsJson)
        {
            Ensure.IsNotNull(entityMap, nameof(entityMap));

            var executionResult = entityMap.GetAstrolabeExecutionResult(
                "errors",
                "events",
                "failures",
                "iterations",
                "successes");

            var events = new BsonArray(executionResult.EventCapturer?.Events?.Select(AstrolabeEventsHandler.CreateEventDocument));

            var eventsDocument = new BsonDocument
            {
                { "events", events },
                { "errors", executionResult.ErrorDocuments ?? new BsonArray() },
                { "failures", executionResult.FailureDocuments ?? new BsonArray() }
            };

            var resultsDocument = new BsonDocument
            {
                { "numErrors", executionResult.ErrorDocuments?.Count ?? 0 },
                { "numFailures", executionResult.FailureDocuments?.Count ?? 0 },
                { "numSuccesses", executionResult.SuccessCount ?? -1 },
                { "numIterations", executionResult.IterationCount ?? -1 }
            };

            var jsonWritterSettings = new JsonWriterSettings
            {
                OutputMode = JsonOutputMode.RelaxedExtendedJson
            };

            eventsJson = eventsDocument.ToJson(jsonWritterSettings);
            resultsJson = resultsDocument.ToJson(jsonWritterSettings);
        }

        private static UnifiedEntityMap ExecuteWorkload(BsonDocument driverWorkload, bool async, CancellationToken cancellationToken)
        {
            var factory = new TestCaseFactory();
            var testCase = factory.CreateTestCase(driverWorkload, async);

            using (var testsExecutor = new UnifiedTestFormatExecutor())
            {
                var entityMap = testsExecutor.Run(testCase);
                Console.WriteLine("dotnet ExecuteWorkload> Returning...");

                return entityMap;
            }
        }

        private static void CancelWorkloadTask(CancellationTokenSource cancellationTokenSource)
        {
            Console.Write($"\ndotnet cancel workload> Canceling the workload task...");
            cancellationTokenSource.Cancel();
            Console.WriteLine($"Done.");
        }

        private static void HandleCancel(
            ConsoleCancelEventArgs args,
            CancellationTokenSource cancellationTokenSource)
        {
            // We set the Cancel property to true to prevent the process from terminating
            args.Cancel = true;
            CancelWorkloadTask(cancellationTokenSource);
        }

        internal class TestCaseFactory : JsonDrivenTestCaseFactory
        {
            public JsonDrivenTestCase CreateTestCase(BsonDocument driverWorkload, bool async)
            {
                var testCase = CreateTestCases(driverWorkload).Single();
                testCase.Test["async"] = async;

                return testCase;
            }

            protected override string GetTestCaseName(BsonDocument shared, BsonDocument test, int index)
            {
                return $"Astrolabe command line arguments:{base.GetTestName(test, index)}";
            }
        }
    }
}
