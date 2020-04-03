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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.TestHelpers.JsonDrivenTests;

namespace MongoDB.Driver.Tests.JsonDrivenTests
{
    public sealed class JsonDrivenCreateIndexTest : JsonDrivenCollectionTest
    {
        // private fields
        private IndexKeysDefinition<BsonDocument> _indexKeys;
        private string _indexName;
        private IClientSessionHandle _session;

        // properties
        private CreateIndexModel<BsonDocument> CreateIndexModel
        {
            get { return new CreateIndexModel<BsonDocument>(_indexKeys, new CreateIndexOptions { Name = _indexName }); }
        }

        // public constructors
        public JsonDrivenCreateIndexTest(IMongoCollection<BsonDocument> collection, Dictionary<string, object> objectMap)
            : base(collection, objectMap)
        {
        }

        // public methods
        public override void Arrange(BsonDocument document)
        {
            JsonDrivenHelper.EnsureAllFieldsAreValid(document, "name", "object", "collectionOptions", "arguments");
            base.Arrange(document);
        }

        // protected methods
        protected override void AssertResult()
        {
        }

        protected override void CallMethod(CancellationToken cancellationToken)
        {
            if (_session == null)
            {
                _collection.Indexes.CreateOne(CreateIndexModel, new CreateOneIndexOptions(), cancellationToken);
            }
            else
            {
                _collection.Indexes.CreateOne(_session, CreateIndexModel, new CreateOneIndexOptions(), cancellationToken);
            }
        }

        protected override async Task CallMethodAsync(CancellationToken cancellationToken)
        {
            if (_session == null)
            {
                await _collection.Indexes.CreateOneAsync(CreateIndexModel, new CreateOneIndexOptions(), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _collection.Indexes.CreateOneAsync(_session, CreateIndexModel, new CreateOneIndexOptions(), cancellationToken).ConfigureAwait(false);
            }
        }

        protected override void SetArgument(string name, BsonValue value)
        {
            switch (name)
            {
                case "keys":
                    _indexKeys = value.AsBsonDocument;
                    return;
                case "name":
                    _indexName = value.AsString;
                    return;
                case "session":
                    _session = (IClientSessionHandle)_objectMap[value.AsString];
                    return;
            }

            base.SetArgument(name, value);
        }
    }
}
