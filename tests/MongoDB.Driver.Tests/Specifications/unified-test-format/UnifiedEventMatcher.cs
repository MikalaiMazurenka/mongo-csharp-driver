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
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver.Core.Events;

namespace MongoDB.Driver.Tests.Specifications.unified_test_format
{
    public class UnifiedEventMatcher
    {
        private UnifiedValueMatcher _valueMatcher;

        public UnifiedEventMatcher(UnifiedValueMatcher valueMatcher)
        {
            _valueMatcher = valueMatcher;
        }

        public void AssertEventsMatch(List<object> actualEvents, BsonArray expectedEventsDocuments)
        {
            actualEvents.Should().HaveSameCount(expectedEventsDocuments);

            for (int i = 0; i < actualEvents.Count; i++)
            {
                var actualEvent = actualEvents[i];
                var expectedEventDocument = expectedEventsDocuments[i].AsBsonDocument;
                expectedEventDocument.ElementCount.Should().Be(1);
                var expectedEventType = expectedEventDocument.GetElement(0).Name;
                var expectedEventValue = expectedEventDocument[0].AsBsonDocument;

                switch (actualEvent)
                {
                    case CommandStartedEvent commandStartedEvent:
                        expectedEventType.Should().Be("commandStartedEvent");
                        foreach (var element in expectedEventValue)
                        {
                            switch (element.Name)
                            {
                                case "command":
                                    _valueMatcher.AssertValuesMatch(commandStartedEvent.Command, element.Value);
                                    break;
                                case "commandName":
                                    commandStartedEvent.CommandName.Should().Be(element.Value.AsString);
                                    break;
                                case "databaseName":
                                    commandStartedEvent.DatabaseNamespace.DatabaseName.Should().Be(element.Value.AsString);
                                    break;
                                default:
                                    throw new FormatException($"Unexpected commandStartedEvent field: '{element.Name}'");
                            }
                        }
                        break;
                    case CommandSucceededEvent commandSucceededEvent:
                        expectedEventType.Should().Be("commandSucceededEvent");
                        foreach (var element in expectedEventValue)
                        {
                            switch (element.Name)
                            {
                                case "reply":
                                    _valueMatcher.AssertValuesMatch(commandSucceededEvent.Reply, element.Value);
                                    break;
                                case "commandName":
                                    commandSucceededEvent.CommandName.Should().Be(element.Value.AsString);
                                    break;
                                default:
                                    throw new FormatException($"Unexpected commandStartedEvent field: '{element.Name}'");
                            }
                        }
                        break;
                    case CommandFailedEvent commandFailedEvent:
                        expectedEventType.Should().Be("commandFailedEvent");
                        foreach (var element in expectedEventValue)
                        {
                            switch (element.Name)
                            {
                                case "commandName":
                                    commandFailedEvent.CommandName.Should().Be(element.Value.AsString);
                                    break;
                                default:
                                    throw new FormatException($"Unexpected commandStartedEvent field: '{element.Name}'");
                            }
                        }
                        break;
                    default:
                        throw new FormatException($"Unrecognized event type: '{actualEvent.GetType()}'");
                }
            }
        }
    }
}
