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
using MongoDB.Bson.TestHelpers.XunitExtensions;
using MongoDB.Driver.Core;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.TestHelpers.JsonDrivenTests;
using MongoDB.Driver.Tests.JsonDrivenTests;
using Xunit;

namespace MongoDB.Driver.Tests.Specifications.atlas_data_lake
{
    [Trait("Category", "AtlasDataLake")]
    public class AtlasDataLakeTestRunner
    {
        #region static
        private static readonly HashSet<string> __commandsToNotCapture = new HashSet<string>
        {
            "configureFailPoint",
            "isMaster",
            "buildInfo",
            "getLastError",
            "authenticate",
            "saslStart",
            "saslContinue",
            "getnonce"
        };
        #endregion

        private readonly EventCapturer _eventCapturer = new EventCapturer()
            .Capture<CommandStartedEvent>(e => !__commandsToNotCapture.Contains(e.CommandName));

        [SkippableTheory]
        [ClassData(typeof(TestCaseFactory))]
        public void Run(JsonDrivenTestCase testCase)
        {
            RunTestDefinition(testCase.Shared, testCase.Test);
        }

        public void RunTestDefinition(BsonDocument shared, BsonDocument test)
        {
            RequireEnvironment.Check().EnvironmentVariable("ATLAS_DATA_LAKE_TESTS_ENABLED");

            JsonDrivenHelper.EnsureAllFieldsAreValid(shared, "_path", "database_name", "collection_name", "tests");
            JsonDrivenHelper.EnsureAllFieldsAreValid(test, "description", "operations", "expectations", "async");

            var databaseName = GetDatabaseName(shared);
            var collectionName = GetCollectionName(shared);

            using (var client = DriverTestConfiguration.CreateDisposableClient(_eventCapturer))
            {
                ExecuteOperation(client, databaseName, collectionName, test, _eventCapturer);
                AssertEventsIfNeeded(_eventCapturer, test);
            }
        }

        private void AssertEvent(object actualEvent, BsonDocument expectedEvent)
        {
            if (expectedEvent.ElementCount != 1)
            {
                throw new FormatException("Expected event must be a document with a single element with a name the specifies the type of the event.");
            }

            var eventType = expectedEvent.GetElement(0).Name;
            var eventAsserter = EventAsserterFactory.CreateAsserter(eventType);
            eventAsserter.AssertAspects(actualEvent, expectedEvent[0].AsBsonDocument);
        }

        private void AssertEventsIfNeeded(EventCapturer eventCapturer, BsonDocument test)
        {
            if (test.TryGetValue("expectations", out var expectations))
            {
                var actualEvents = eventCapturer.Events;
                var expectedEvents = expectations.AsBsonArray.Cast<BsonDocument>().ToList();

                var minCount = Math.Min(actualEvents.Count, expectedEvents.Count);
                for (var i = 0; i < minCount; i++)
                {
                    AssertEvent(actualEvents[i], expectedEvents[i]);
                }

                if (actualEvents.Count < expectedEvents.Count)
                {
                    throw new Exception($"Missing event: {expectedEvents[actualEvents.Count]}.");
                }

                if (actualEvents.Count > expectedEvents.Count)
                {
                    throw new Exception($"Unexpected event of type: {actualEvents[expectedEvents.Count].GetType().Name}.");
                }
            }
        }
        
        private void ExecuteOperation(IMongoClient client, string databaseName, string collectionName, BsonDocument test, EventCapturer eventCapturer)
        {
            var factory = new JsonDrivenTestFactory(client, databaseName, collectionName, bucketName: null, objectMap: null, eventCapturer);

            foreach (var operation in test["operations"].AsBsonArray.Cast<BsonDocument>())
            {
                JsonDrivenHelper.EnsureAllFieldsAreValid(operation, "name", "object", "command_name", "arguments", "result");

                var receiver = operation["object"].AsString;
                var name = operation["name"].AsString;
                var jsonDrivenTest = factory.CreateTest(receiver, name);
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

        private string GetCollectionName(BsonDocument definition)
        {
            if (definition.TryGetValue("collection_name", out var collectionName))
            {
                return collectionName.AsString;
            }
            else
            {
                return DriverTestConfiguration.CollectionNamespace.CollectionName;
            }
        }

        private string GetDatabaseName(BsonDocument definition)
        {
            if (definition.TryGetValue("database_name", out var databaseName))
            {
                return databaseName.AsString;
            }
            else
            {
                return DriverTestConfiguration.DatabaseNamespace.DatabaseName;
            }
        }

        // nested types
        private class TestCaseFactory : JsonDrivenTestCaseFactory
        {
            // protected properties
            protected override string PathPrefix => "MongoDB.Driver.Tests.Specifications.atlas_data_lake.tests.";

            // protected methods
            protected override IEnumerable<JsonDrivenTestCase> CreateTestCases(BsonDocument document)
            {
                foreach (var testCase in base.CreateTestCases(document))
                {
                    foreach (var async in new[] { false, true })
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
