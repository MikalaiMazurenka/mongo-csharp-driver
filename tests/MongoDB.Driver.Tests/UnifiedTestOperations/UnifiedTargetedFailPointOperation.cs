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
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver.Core.Bindings;
using MongoDB.Driver.Core.TestHelpers;
using MongoDB.Driver.Tests.Specifications.unified_test_format;

namespace MongoDB.Driver.Tests.UnifiedTestOperations
{
    public class UnifiedTargetedFailPointOperation : IUnifiedFailPointOperation
    {
        private readonly BsonDocument _failPointCommand;
        private readonly IClientSessionHandle _session;

        public UnifiedTargetedFailPointOperation(
            IClientSessionHandle session,
            BsonDocument failPointCommand)
        {
            _session = session;
            _failPointCommand = failPointCommand;
        }

        public void Execute(out FailPoint failPoint)
        {
            var pinnedServer = _session?.WrappedCoreSession?.CurrentTransaction?.PinnedServer;
            pinnedServer.Should().NotBeNull();
            var session = NoCoreSession.NewHandle();

            failPoint = FailPoint.Configure(pinnedServer, session, _failPointCommand);
        }
    }

    public class UnifiedTargetedFailPointOperationBuilder
    {
        private readonly EntityMap _entityMap;

        public UnifiedTargetedFailPointOperationBuilder(EntityMap entityMap)
        {
            _entityMap = entityMap;
        }

        public UnifiedTargetedFailPointOperation Build(BsonDocument arguments)
        {
            BsonDocument failPointCommand = null;
            IClientSessionHandle session = null;

            foreach (var argument in arguments)
            {
                switch (argument.Name)
                {
                    case "failPoint":
                        failPointCommand = argument.Value.AsBsonDocument;
                        break;
                    case "session":
                        var sessionId = argument.Value.AsString;
                        session = _entityMap.GetSession(sessionId);
                        break;
                    default:
                        throw new FormatException($"Invalid TargetedFailPointOperation argument name: '{argument.Name}'");
                }
            }

            return new UnifiedTargetedFailPointOperation(session, failPointCommand);
        }
    }
}
