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
using MongoDB.Bson;
using MongoDB.Driver.Core;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.GridFS;
using MongoDB.Driver.TestHelpers;

namespace MongoDB.Driver.Tests.Specifications.unified_test_format
{
    public sealed class EntityMap : IDisposable
    {
        // private variables
        private readonly Dictionary<string, IGridFSBucket> _buckets = new Dictionary<string, IGridFSBucket>();
        private readonly Dictionary<string, IEnumerator<ChangeStreamDocument<BsonDocument>>> _changeStreams = new Dictionary<string, IEnumerator<ChangeStreamDocument<BsonDocument>>>();
        private readonly Dictionary<string, EventCapturer> _clientEventCapturers = new Dictionary<string, EventCapturer>();
        private readonly Dictionary<string, DisposableMongoClient> _clients = new Dictionary<string, DisposableMongoClient>();
        private readonly Dictionary<string, IMongoCollection<BsonDocument>> _collections = new Dictionary<string, IMongoCollection<BsonDocument>>();
        private readonly Dictionary<string, IMongoDatabase> _databases = new Dictionary<string, IMongoDatabase>();
        private readonly Dictionary<string, BsonValue> _results = new Dictionary<string, BsonValue>();
        private readonly Dictionary<string, IClientSessionHandle> _sessions = new Dictionary<string, IClientSessionHandle>();
        private readonly Dictionary<string, BsonDocument> _sessionIds = new Dictionary<string, BsonDocument>();

        public EntityMap(BsonArray entitiesArray)
        {
            foreach (var entityItem in entitiesArray)
            {
                if (entityItem.AsBsonDocument.ElementCount != 1)
                {
                    throw new FormatException("Entity item should contain single element");
                }

                var entityType = entityItem.AsBsonDocument.GetElement(0).Name;
                var entity = entityItem[0].AsBsonDocument;
                var id = entity["id"].AsString;
                switch (entityType)
                {
                    case "bucket":
                        if (_buckets.ContainsKey(id))
                        {
                            throw new Exception($"Bucket entity with id '{id}' already exists");
                        }
                        var bucket = CreateBucket(entity);
                        _buckets.Add(id, bucket);
                        break;
                    case "client":
                        if (_clients.ContainsKey(id))
                        {
                            throw new Exception($"Client entity with id '{id}' already exists");
                        }
                        var createClientResult = CreateClient(entity);
                        _clients.Add(id, createClientResult.Item1);
                        _clientEventCapturers.Add(id, createClientResult.Item2);
                        break;
                    case "collection":
                        if (_collections.ContainsKey(id))
                        {
                            throw new Exception($"Collection entity with id '{id}' already exists");
                        }
                        var collection = CreateCollection(entity);
                        _collections.Add(id, collection);
                        break;
                    case "database":
                        if (_databases.ContainsKey(id))
                        {
                            throw new Exception($"Database entity with id '{id}' already exists");
                        }
                        var database = CreateDatabase(entity);
                        _databases.Add(id, database);
                        break;
                    case "session":
                        if (_sessions.ContainsKey(id))
                        {
                            throw new Exception($"Session entity with id '{id}' already exists");
                        }
                        var createSessionResult = CreateSession(entity);
                        _sessions.Add(id, createSessionResult.Item1);
                        _sessionIds.Add(id, createSessionResult.Item2);
                        break;
                    default:
                        throw new FormatException($"Unrecognized entity type: '{entityType}'");
                }
            }
        }

        // public methods
        public void AddChangeStream(string changeStreamId, IEnumerator<ChangeStreamDocument<BsonDocument>> changeStream)
        {
            _changeStreams.Add(changeStreamId, changeStream);
        }

        public void AddResult(string resultId, BsonValue value)
        {
            _results.Add(resultId, value);
        }

        public void Dispose()
        {
            if (_changeStreams != null)
            {
                foreach (var changeStream in _changeStreams.Values)
                {
                    changeStream?.Dispose();
                }
            }
            if (_sessions != null)
            {
                foreach (var session in _sessions.Values)
                {
                    session?.Dispose();
                }
            }
            if (_clients != null)
            {
                foreach (var client in _clients.Values)
                {
                    client?.Dispose();
                }
            }
        }

        public IGridFSBucket GetBucket(string bucketId)
        {
            return _buckets[bucketId];
        }

        public IEnumerator<ChangeStreamDocument<BsonDocument>> GetChangeStream(string changeStreamId)
        {
            return _changeStreams[changeStreamId];
        }

        public IMongoClient GetClient(string clientId)
        {
            return _clients[clientId];
        }

        public IMongoCollection<BsonDocument> GetCollection(string collectionId)
        {
            return _collections[collectionId];
        }

        public IMongoDatabase GetDatabase(string databaseId)
        {
            return _databases[databaseId];
        }

        public EventCapturer GetEventCapturer(string clientId)
        {
            return _clientEventCapturers[clientId];
        }

        public BsonValue GetResult(string resultId)
        {
            return _results[resultId];
        }

        public IClientSessionHandle GetSession(string sessionId)
        {
            return _sessions[sessionId];
        }

        public BsonDocument GetSessionId(string sessionId)
        {
            return _sessionIds[sessionId];
        }

        public bool HasBucket(string bucketId)
        {
            return _buckets.ContainsKey(bucketId);
        }

        public bool HasChangeStream(string changeStreamId)
        {
            return _changeStreams.ContainsKey(changeStreamId);
        }

        public bool HasClient(string clientId)
        {
            return _clients.ContainsKey(clientId);
        }

        public bool HasCollection(string collectionId)
        {
            return _collections.ContainsKey(collectionId);
        }

        public bool HasDatabase(string databaseId)
        {
            return _databases.ContainsKey(databaseId);
        }

        public bool HasSession(string sessionId)
        {
            return _sessions.ContainsKey(sessionId);
        }

        // private methods
        private IGridFSBucket CreateBucket(BsonDocument entity)
        {
            IMongoDatabase database = null;

            foreach (var element in entity)
            {
                switch (element.Name)
                {
                    case "id":
                        // handled on higher level
                        break;
                    case "database":
                        var databaseId = element.Value.AsString;
                        database = _databases[databaseId];
                        break;
                    default:
                        throw new FormatException($"Unrecognized bucket entity field: '{element.Name}'");
                }
            }

            return new GridFSBucket(database);
        }

        private (DisposableMongoClient, EventCapturer) CreateClient(BsonDocument entity)
        {
            EventCapturer eventCapturer = null;
            var eventTypesToCapture = new List<string>();
            var commandNamesToSkip = new List<string> { "configureFailPoint", "buildInfo", "isMaster" };

            var readConcern = ReadConcern.Default;
            var retryReads = true;
            var retryWrites = true;
            var useMultipleShardRouters = false;
            var writeConcern = WriteConcern.Acknowledged;

            foreach (var element in entity)
            {
                switch (element.Name)
                {
                    case "id":
                        // handled on higher level
                        break;
                    case "useMultipleMongoses":
                        useMultipleShardRouters = element.Value.AsBoolean;
                        break;
                    case "observeEvents":
                        eventCapturer = eventCapturer ?? new EventCapturer();
                        eventTypesToCapture.AddRange(element.Value.AsBsonArray.Select(x => x.AsString));
                        break;
                    case "ignoreCommandMonitoringEvents":
                        eventCapturer = eventCapturer ?? new EventCapturer();
                        commandNamesToSkip.AddRange(element.Value.AsBsonArray.Select(x => x.AsString));
                        break;
                    case "uriOptions":
                        foreach (var option in element.Value.AsBsonDocument)
                        {
                            switch (option.Name)
                            {
                                case "retryWrites":
                                    retryWrites = option.Value.AsBoolean;
                                    break;
                                case "retryReads":
                                    retryReads = option.Value.AsBoolean;
                                    break;
                                case "readConcernLevel":
                                    var levelValue = option.Value.AsString;
                                    var level = (ReadConcernLevel)Enum.Parse(typeof(ReadConcernLevel), levelValue, true);
                                    readConcern = new ReadConcern(level);
                                    break;
                                case "w":
                                    writeConcern = new WriteConcern(option.Value.AsInt32);
                                    break;
                                default:
                                    throw new FormatException($"Unrecognized client uriOption name: '{option.Name}'");
                            }
                        }
                        break;
                    default:
                        throw new FormatException($"Unrecognized client entity field: '{element.Name}'");
                }
            }

            if (eventCapturer != null)
            {
                foreach (var eventTypeToCapture in eventTypesToCapture)
                {
                    switch (eventTypeToCapture)
                    {
                        case "commandStartedEvent":
                            eventCapturer = eventCapturer.Capture<CommandStartedEvent>(x => !commandNamesToSkip.Contains(x.CommandName));
                            break;
                        case "commandSucceededEvent":
                            eventCapturer = eventCapturer.Capture<CommandSucceededEvent>(x => !commandNamesToSkip.Contains(x.CommandName));
                            break;
                        case "commandFailedEvent":
                            eventCapturer = eventCapturer.Capture<CommandFailedEvent>(x => !commandNamesToSkip.Contains(x.CommandName));
                            break;
                        default:
                            throw new FormatException($"Invalid event name: {eventTypeToCapture}");
                    }
                }
            }

            var client = DriverTestConfiguration.CreateDisposableClient(
                settings =>
                {
                    settings.RetryReads = retryReads;
                    settings.RetryWrites = retryWrites;
                    settings.ReadConcern = readConcern;
                    settings.WriteConcern = writeConcern;
                    settings.HeartbeatInterval = TimeSpan.FromMilliseconds(5); // the default value for spec tests
                    if (eventCapturer != null)
                    {
                        settings.ClusterConfigurator = c => c.Subscribe(eventCapturer);
                    }
                },
                useMultipleShardRouters);

            return (client, eventCapturer);
        }

        private IMongoCollection<BsonDocument> CreateCollection(BsonDocument entity)
        {
            string collectionName = null;
            IMongoDatabase database = null;
            MongoCollectionSettings settings = null;

            foreach (var element in entity)
            {
                switch (element.Name)
                {
                    case "id":
                        // handled on higher level
                        break;
                    case "database":
                        var databaseId = entity["database"].AsString;
                        database = _databases[databaseId];
                        break;
                    case "collectionName":
                        collectionName = entity["collectionName"].AsString;
                        break;
                    case "collectionOptions":
                        settings = new MongoCollectionSettings();
                        foreach (var option in element.Value.AsBsonDocument)
                        {
                            switch (option.Name)
                            {
                                case "readConcern":
                                    settings.ReadConcern = ReadConcern.FromBsonDocument(option.Value.AsBsonDocument);
                                    break;
                                default:
                                    throw new FormatException($"Unrecognized collection option field: '{option.Name}'");
                            }
                        }
                        break;
                    default:
                        throw new FormatException($"Unrecognized collection entity field: '{element.Name}'");
                }
            }

            return database.GetCollection<BsonDocument>(collectionName, settings);
        }

        private IMongoDatabase CreateDatabase(BsonDocument entity)
        {
            IMongoClient client = null;
            string databaseName = null;

            foreach (var element in entity)
            {
                switch (element.Name)
                {
                    case "id":
                        // handled on higher level
                        break;
                    case "client":
                        var clientId = element.Value.AsString;
                        client = _clients[clientId];
                        break;
                    case "databaseName":
                        databaseName = element.Value.AsString;
                        break;
                    default:
                        throw new FormatException($"Unrecognized database entity field: '{element.Name}'");
                }
            }

            return client.GetDatabase(databaseName);
        }

        private (IClientSessionHandle, BsonDocument) CreateSession(BsonDocument entity)
        {
            IMongoClient client = null;
            ClientSessionOptions options = null;

            foreach (var element in entity)
            {
                switch (element.Name)
                {
                    case "id":
                        // handled on higher level
                        break;
                    case "client":
                        var clientId = element.Value.AsString;
                        client = _clients[clientId];
                        break;
                    case "sessionOptions":
                        options = new ClientSessionOptions();
                        foreach (var option in element.Value.AsBsonDocument)
                        {
                            switch (option.Name)
                            {
                                case "causalConsistency":
                                    options.CausalConsistency = option.Value.ToBoolean();
                                    break;
                                case "defaultTransactionOptions":
                                    ReadConcern readConcern = null;
                                    ReadPreference readPreference = null;
                                    WriteConcern writeConcern = null;
                                    foreach (var transactionOption in option.Value.AsBsonDocument)
                                    {
                                        switch (transactionOption.Name)
                                        {
                                            case "readConcern":
                                                readConcern = ReadConcern.FromBsonDocument(transactionOption.Value.AsBsonDocument);
                                                break;
                                            case "readPreference":
                                                readPreference = ReadPreference.FromBsonDocument(transactionOption.Value.AsBsonDocument);
                                                break;
                                            case "writeConcern":
                                                writeConcern = WriteConcern.FromBsonDocument(transactionOption.Value.AsBsonDocument);
                                                break;
                                            default:
                                                throw new FormatException($"Invalid session transaction option: '{transactionOption.Name}'");
                                        }
                                    }
                                    options.DefaultTransactionOptions = new TransactionOptions(readConcern, readPreference, writeConcern);
                                    break;
                                default:
                                    throw new FormatException($"Unrecognized session option: '{option.Name}'");
                            }
                        }
                        break;
                    default:
                        throw new FormatException($"Unrecognized database entity field: '{element.Name}'");
                }
            }

            var session = client.StartSession(options);
            var sessionId = session.WrappedCoreSession.Id;

            return (session, sessionId);
        }
    }
}
