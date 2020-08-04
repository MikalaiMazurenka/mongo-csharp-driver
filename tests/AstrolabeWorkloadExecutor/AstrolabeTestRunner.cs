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
        // private fields
        private readonly CancellationToken _cancellationToken;
        private readonly Action _incrementOperationSuccesses;
        private readonly Action _incrementOperationErrors;
        private readonly Action _incrementOperationFailures;

        // protected properties
        protected override string[] ExpectedSharedColumns => new[] { "_path", "database", "collection", "testData", "tests" };
        protected override string[] ExpectedTestColumns => new[] { "operations", "async" };

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

        // protected methods
        protected override void ExecuteOperations(IMongoClient client, Dictionary<string, object> objectMap, BsonDocument test, EventCapturer eventCapturer = null)
        {
            _objectMap = objectMap;

            var factory = new JsonDrivenTestFactory(client, DatabaseName, CollectionName, bucketName: null, objectMap, eventCapturer);

            Func<JsonDrivenTest, AstrolabeJsonDrivenTest> wrapTest = wrapped =>
                new AstrolabeJsonDrivenTest(wrapped, _incrementOperationSuccesses, _incrementOperationErrors, _incrementOperationFailures);

            foreach (var operation in test[OperationsKey].AsBsonArray.Cast<BsonDocument>())
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    return;
                }

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
                    // Run unknown commands via runCommand
                    var database = client.GetDatabase(DatabaseName);
                    var innerTest = new JsonDrivenRunCommandTest(database, objectMap);
                    operation["command_name"] = operation["name"];
                    operation["object"] = "database";
                    var command = new BsonDocument(operation["name"].AsString, 1);
                    if (operation.TryGetValue("arguments", out var argumentsValue) && argumentsValue is BsonDocument arguments)
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

        protected override void RunTest(BsonDocument shared, BsonDocument test, EventCapturer eventCapturer)
        {
            Console.WriteLine("dotnet astrolabetestrunner> creating disposable client...");
            using (var client = CreateClient(eventCapturer))
            {
                Console.WriteLine("dotnet astrolabetestrunner> looping until cancellation is requested...");
                while(!_cancellationToken.IsCancellationRequested)
                {
                    // Clone because inserts will auto assign an id to the test case document
                    ExecuteOperations(
                        client: client,
                        objectMap: new Dictionary<string, object>(),
                        test: test.DeepClone().AsBsonDocument);
                }
            }

            DisposableMongoClient CreateClient(EventCapturer eventCapturer)
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

        // nested types
        internal class TestCaseFactory : JsonDrivenTestCaseFactory
        {
            public JsonDrivenTestCase CreateTestCase(BsonDocument driverWorkload, bool async)
            {
                JsonDrivenHelper.EnsureAllFieldsAreValid(driverWorkload, new [] { "database", "collection", "testData", "operations" });

                var adaptedDriverWorkload = new BsonDocument
                {
                    { "_path", "Astrolabe command line arguments" },
                    { "database", driverWorkload["database"] },
                    { "collection", driverWorkload["collection"] },
                    { "tests", new BsonArray(new [] { new BsonDocument("operations", driverWorkload["operations"]) }) }
                };
                if (driverWorkload.Contains("testData"))
                {
                    adaptedDriverWorkload.Add("testData", driverWorkload["testData"]);
                }
                var testCase = CreateTestCases(adaptedDriverWorkload).Single();
                testCase.Test["async"] = async;

                return testCase;
            }
        }
    }
}
