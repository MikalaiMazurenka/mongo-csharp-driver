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
using System.Linq;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver.Core;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Tests.Specifications.unified_test_format;

namespace MongoDB.Driver.Tests.UnifiedTestOperations
{
    public class UnifiedAssertDifferentLsidOnLastTwoCommandsOperation : IUnifiedSpecialTestOperation
    {
        private readonly EventCapturer _eventCapturer;

        public UnifiedAssertDifferentLsidOnLastTwoCommandsOperation(EventCapturer eventCapturer)
        {
            _eventCapturer = eventCapturer;
        }

        public void Execute()
        {
            var lastTwoCommands = _eventCapturer
                .Events
                .Skip(_eventCapturer.Events.Count - 2)
                .Select(commandStartedEvent => ((CommandStartedEvent)commandStartedEvent).Command)
                .ToList();

            AssertDifferentLsid(lastTwoCommands[0], lastTwoCommands[1]);
        }

        // private methods
        private void AssertDifferentLsid(BsonDocument first, BsonDocument second)
        {
            first["lsid"].Should().NotBe(second["lsid"]);
        }
    }

    public class UnifiedAssertDifferentLsidOnLastTwoCommandsOperationBuilder
    {
        private readonly EntityMap _entityMap;

        public UnifiedAssertDifferentLsidOnLastTwoCommandsOperationBuilder(EntityMap entityMap)
        {
            _entityMap = entityMap;

        }

        public UnifiedAssertDifferentLsidOnLastTwoCommandsOperation Build(BsonDocument arguments)
        {
            EventCapturer eventCapturer = null;

            foreach (var argument in arguments)
            {
                switch (argument.Name)
                {
                    case "client":
                        eventCapturer = _entityMap.GetEventCapturer(argument.Value.AsString);
                        break;
                    default:
                        throw new FormatException($"Invalid AssertDifferentLsidOnLastTwoCommandsOperation argument name: '{argument.Name}'");
                }
            }

            return new UnifiedAssertDifferentLsidOnLastTwoCommandsOperation(eventCapturer);
        }
    }
}
