/* Copyright 2021-present MongoDB Inc.
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
using System.Net;
using MongoDB.Bson;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.Servers;

namespace AstrolabeWorkloadExecutor
{
    public class AstrolabeEventConverter
    {
        public BsonDocument Convert(dynamic @event)
        {
            switch (@event.GetType())
            {
                case ConnectionPoolOpenedEvent connectionPoolOpenedEvent:
                    return CreateCmapEventDocument("poolCreatedEvent", connectionPoolOpenedEvent.ObservedAt, connectionPoolOpenedEvent.ServerId);
                case ConnectionPoolClearedEvent connectionPoolClearedEvent:
                    return CreateCmapEventDocument("poolClearedEvent", connectionPoolClearedEvent.ObservedAt, connectionPoolClearedEvent.ServerId);
                case ConnectionPoolClosedEvent connectionPoolClosedEvent:
                    return CreateCmapEventDocument("poolClosedEvent", connectionPoolClosedEvent.ObservedAt, connectionPoolClosedEvent.ServerId);
                case ConnectionCreatedEvent connectionCreatedEvent:
                    return CreateCmapEventDocument("connectionCreatedEvent", connectionCreatedEvent.ObservedAt, connectionCreatedEvent.ConnectionId);
                case ConnectionClosedEvent connectionClosedEvent:
                    return CreateCmapEventDocument("connectionClosedEvent", connectionClosedEvent.ObservedAt, connectionClosedEvent.ConnectionId);
                case ConnectionPoolCheckingOutConnectionEvent connectionPoolCheckingOutConnectionEvent:
                    return CreateCmapEventDocument("connectionCheckOutStartedEvent", connectionPoolCheckingOutConnectionEvent.ObservedAt, connectionPoolCheckingOutConnectionEvent.ServerId);
                case ConnectionPoolCheckingOutConnectionFailedEvent connectionPoolCheckingOutConnectionFailedEvent:
                    return CreateCmapEventDocument("connectionCheckOutFailedEvent", connectionPoolCheckingOutConnectionFailedEvent.ObservedAt, connectionPoolCheckingOutConnectionFailedEvent.ServerId);
                case ConnectionPoolCheckedOutConnectionEvent connectionPoolCheckedOutConnectionEvent:
                    return CreateCmapEventDocument("connectionCheckedOutEvent", connectionPoolCheckedOutConnectionEvent.ObservedAt, connectionPoolCheckedOutConnectionEvent.ConnectionId);
                case ConnectionPoolCheckedInConnectionEvent connectionPoolCheckedInConnectionEvent:
                    return CreateCmapEventDocument("connectionCheckedInEvent", connectionPoolCheckedInConnectionEvent.ObservedAt, connectionPoolCheckedInConnectionEvent.ConnectionId);
                case CommandStartedEvent commandStartedEvent:
                    return CreateCommandEventDocument("commandStartedEvent", commandStartedEvent.ObservedAt, commandStartedEvent.CommandName, commandStartedEvent.RequestId);
                case CommandSucceededEvent commandSucceededEvent:
                    return CreateCommandEventDocument("commandSucceededEvent", commandSucceededEvent.ObservedAt, commandSucceededEvent.CommandName, commandSucceededEvent.RequestId);
                case CommandFailedEvent commandFailedEvent:
                    return CreateCommandEventDocument("commandFailedEvent", commandFailedEvent.ObservedAt, commandFailedEvent.CommandName, commandFailedEvent.RequestId);
                default:
                    throw new FormatException($"Unrecognized event type: '{@event.GetType()}'.");
            }
        }

        // private methods
        private BsonDocument CreateCmapEventDocument(string eventName, DateTime observedAt, ServerId serverId)
        {
            return new BsonDocument
            {
                { "name", eventName },
                { "observedAt", GetCurrentTimeSeconds(observedAt) },
                { "address", GetAddress(serverId) }
            };
        }

        private BsonDocument CreateCmapEventDocument(string eventName, DateTime observedAt, ConnectionId connectionId)
        {
            return new BsonDocument
            {
                { "name", eventName },
                { "observedAt", GetCurrentTimeSeconds(observedAt) },
                { "address", GetAddress(connectionId.ServerId) },
                { "connectionId", connectionId.LocalValue }
            };
        }

        private BsonDocument CreateCommandEventDocument(string eventName, DateTime observedAt, string commandName, int requestId)
        {
            return new BsonDocument
            {
                { "name", eventName },
                { "observedAt", GetCurrentTimeSeconds(observedAt) },
                { "commandName", commandName },
                { "requestId", requestId }
            };
        }

        private string GetAddress(ServerId serverId)
        {
            var endpoint = serverId.EndPoint;

            return ((DnsEndPoint)endpoint).Host + ":" + ((DnsEndPoint)endpoint).Port;
        }

        private double GetCurrentTimeSeconds(DateTime observedAt)
        {
            return (double)(observedAt - BsonConstants.UnixEpoch).TotalMilliseconds / 1000;
        }
    }
}
