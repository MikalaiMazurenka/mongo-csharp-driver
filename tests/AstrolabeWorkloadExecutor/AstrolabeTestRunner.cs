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
using MongoDB.Bson.TestHelpers;
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

            foreach (var operation in test[OperationsKey].AsBsonArray.Cast<BsonDocument>())
            {
                ModifyOperationIfNeeded(operation);
                var receiver = operation["object"].AsString;
                var name = operation["name"].AsString;
                JsonDrivenTest jsonDrivenTest = factory.CreateTest(receiver, name);
                jsonDrivenTest.Arrange(operation);
                if (test["async"].AsBoolean)
                {
                    jsonDrivenTest.ActAsync(_cancellationToken).GetAwaiter().GetResult();
                }
                else
                {
                    jsonDrivenTest.Act(_cancellationToken);
                }
                AssertTest(jsonDrivenTest);
            }
        }

        public void AssertTest(JsonDrivenTest test)
        {
            var wrappedActualException = test._actualException();
            if (test._expectedException() == null)
            {
                if (wrappedActualException != null)
                {
                    if (!(wrappedActualException is OperationCanceledException))
                    {
                        Console.WriteLine($"Operation error (unexpected exception): {wrappedActualException}");
                        _incrementOperationErrors();
                    }

                    return;
                }
                if (test._expectedResult() == null)
                {
                    _incrementOperationSuccesses();
                }
                else
                {
                    try
                    {
                        test.AssertResult();
                        _incrementOperationSuccesses();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Operation failure (unexpected result): {ex}");
                        _incrementOperationFailures();
                    }
                }
            }
            else
            {
                if (wrappedActualException == null)
                {
                    _incrementOperationErrors();

                    return;
                }
                try
                {
                    test.AssertException();
                    _incrementOperationSuccesses();
                }
                catch
                {
                    _incrementOperationFailures();
                }
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

    internal static class JsonDrivenTestReflector
    {
        public static Exception _actualException(this JsonDrivenTest test)
        {
            return (Exception)Reflector.GetFieldValue(test, nameof(_actualException));
        }

        public static BsonValue _expectedResult(this JsonDrivenTest test)
        {
            return (BsonValue)Reflector.GetFieldValue(test, nameof(_expectedResult));
        }

        public static BsonDocument _expectedException(this JsonDrivenTest test)
        {
            return (BsonDocument)Reflector.GetFieldValue(test, nameof(_expectedException));
        }

        public static void AssertException(this JsonDrivenTest test)
        {
            Reflector.Invoke(test, nameof(AssertException));
        }

        public static void AssertResult(this JsonDrivenTest test)
        {
            Reflector.Invoke(test, nameof(AssertResult));
        }

        public static void CallMethod(this JsonDrivenTest test, CancellationToken cancellationToken)
        {
            Reflector.Invoke(test, nameof(CallMethod), cancellationToken);
        }

        public static Task CallMethodAsync(this JsonDrivenTest test, CancellationToken cancellationToken)
        {
            return (Task)Reflector.Invoke(test, nameof(CallMethodAsync), cancellationToken);
        }

        public static void ParseExpectedResult(this JsonDrivenTest test, BsonValue value)
        {
           Reflector.Invoke(test, nameof(ParseExpectedResult), value);
        }

        public static void SetArgument(this JsonDrivenTest test, string name, BsonValue value)
        {
           Reflector.Invoke(test, nameof(SetArgument), name, value);
        }

        public static void SetArguments(this JsonDrivenTest test, BsonDocument arguments)
        {
           Reflector.Invoke(test, nameof(SetArguments), arguments);
        }
    }
}
