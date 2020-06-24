/* Copyright 2020-present MongoDB Inc.
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.TestHelpers.JsonDrivenTests;
using MongoDB.Driver;
using MongoDB.Driver.Core;
using MongoDB.Driver.TestHelpers;
using MongoDB.Driver.Tests.JsonDrivenTests;
using MongoDB.Driver.Tests.Specifications.Runner;

namespace WorkloadExecutor
{
    public class AstrolabeTestRunner : MongoClientJsonDrivenTestRunnerBase
    {
        protected override string[] ExpectedSharedColumns => new[] { "_path", "description", "database", "collection", "testData", "tests", "operations", "outcome", "expectations", "async" };
        protected override string[] ExpectedTestColumns => new[] { "description", "database", "collection", "testData", "operations", "outcome", "expectations", "async" };

        private readonly Action _incrementOperationSuccesses;
        private readonly Action _incrementOperationErrors;
        private readonly Action _incrementOperationFailures;
        private readonly CancellationToken _cancellationToken;

        public AstrolabeTestRunner(
            Action incrementOperationSuccesses,
            Action incrementOperationErrors,
            Action incrementOperationFailures,
            CancellationToken cancellationToken)
        {
            _incrementOperationSuccesses = incrementOperationSuccesses;
            _incrementOperationErrors = incrementOperationErrors;
            _incrementOperationFailures = incrementOperationFailures;

            _cancellationToken = cancellationToken;
        }

        protected override string DatabaseNameKey => "database";
        protected override string CollectionNameKey => "collection";
        protected override string DataKey => "testData";

        // public methods
        public void Run(JsonDrivenTestCase testCase)
        {
            SetupAndRunTest(testCase);
        }

        public async Task RunAsync(JsonDrivenTestCase testCase)
        {
            await Task.Run(()=>SetupAndRunTest(testCase), CancellationToken.None).ConfigureAwait(false); // SetupAndRunTest runs synchronously
        }

        protected override void RunTest(BsonDocument shared, BsonDocument test, EventCapturer eventCapturer)
        {
            Console.WriteLine("dotnet astrolabetestrunner> creating disposable client...");
            using (var client = CreateClient(eventCapturer)) // DriverTestConfiguration.CreateDisposableClient does not work with mongodb+srv
            {
                Console.WriteLine("dotnet astrolabetestrunner> looping until cancellation is requested...");
                while(!_cancellationToken.IsCancellationRequested)
                {
                    // we clone because inserts will auto assign an id to the test case document
                    ExecuteOperations(client,  objectMap: new Dictionary<string, object>(), test.DeepClone().AsBsonDocument);
                }
            }

            static DisposableMongoClient CreateClient(EventCapturer eventCapturer)
            {
                var connectionString = Environment.GetEnvironmentVariable("MONGODB_URI");
                var settings = MongoClientSettings.FromConnectionString(connectionString);
                if (eventCapturer != null)
                {
                    settings.ClusterConfigurator = c => c.Subscribe(eventCapturer);
                }

                return new DisposableMongoClient(new MongoClient(settings));
            }
        }

        protected override void ExecuteOperations(IMongoClient client, Dictionary<string, object> objectMap, BsonDocument test, EventCapturer? eventCapturer = null)
        {
            _objectMap = objectMap;

            var factory = new JsonDrivenTestFactory(client, DatabaseName, CollectionName, bucketName: null, objectMap,
                eventCapturer);

            Func<JsonDrivenTest, AstrolabeJsonDrivenTest> wrapTest = wrapped =>
                new AstrolabeJsonDrivenTest(wrapped, _incrementOperationSuccesses, _incrementOperationErrors, _incrementOperationFailures);

            foreach (var operation in test[OperationsKey].AsBsonArray.Cast<BsonDocument>())
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                ModifyOperationIfNeeded(operation);
                var receiver = operation["object"].AsString;
                var name = operation["name"].AsString;
                JsonDrivenTest jsonDrivenTest;
                try
                {
                    var innerTest = factory.CreateTest(receiver, name);
                    jsonDrivenTest = wrapTest(innerTest);
                }
                catch (FormatException)
                {
                    // run unknown commands via runCommand
                    var database = client.GetDatabase(DatabaseName);
                    var innerTest = new JsonDrivenRunCommandTest(database, objectMap);
                    operation["command_name"] = operation["name"];
                    operation["object"] = "database"; // JsonDrivenRunCommand requires this
                    var command = new BsonDocument(operation["name"].AsString, 1);
                    if (operation.TryGetValue("arguments", out var argumentsValue) &&
                        argumentsValue is BsonDocument arguments)
                    {
                        command.Merge(arguments);
                        operation.Remove("arguments");
                    }
                    operation.Add("arguments", new BsonDocument("command", command));
                    jsonDrivenTest = wrapTest(innerTest);
                }

                jsonDrivenTest.Arrange(operation);
                if (test["async"].AsBoolean)
                {
                    jsonDrivenTest.ActAsync(CancellationToken.None).GetAwaiter().GetResult();
                }
                else
                {
                    jsonDrivenTest.Act(CancellationToken.None);
                }

                jsonDrivenTest.Assert();
            }
        }

        // nested types
        public class TestCaseFactory : JsonDrivenTestCaseFactory
        {
            public JsonDrivenTestCase CreateTestCase(BsonDocument driverWorkload)
            {
                var adaptedDriverWorkload = new BsonDocument("tests", new BsonArray(new [] {driverWorkload}));
                adaptedDriverWorkload.Add("_path", "Astrolabe command line arguments");
                adaptedDriverWorkload.Add("database", driverWorkload["database"]); // todo: drop these?
                adaptedDriverWorkload.Add("collection", driverWorkload["collection"]);
                if (driverWorkload.Contains("testData"))
                {
                    adaptedDriverWorkload.Add("testData", driverWorkload["testData"]);
                }
                var testCases = CreateTestCases(adaptedDriverWorkload).ToList();
                if (testCases.Count != 1)
                {
                    throw new Exception($"{nameof(driverWorkload)} should only have one test.");
                }
                return testCases[0];
            }

            // protected properties
            protected override string? PathPrefix => null;

            // protected methods
            protected override IEnumerable<JsonDrivenTestCase> CreateTestCases(BsonDocument document)
            {
                foreach (var testCase in base.CreateTestCases(document))
                {
                    foreach (var async in new[] { true })
                    {
                        var name = $"{testCase.Name}:async={async}";
                        var test = testCase.Test.DeepClone().AsBsonDocument.Add("async", async);
                        yield return new JsonDrivenTestCase(name, testCase.Shared, test);
                    }
                }
            }
        }
    }
}
