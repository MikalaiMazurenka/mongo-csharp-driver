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
using System.Threading;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.TestHelpers.XunitExtensions;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Core.TestHelpers.XunitExtensions;
using Xunit;

namespace MongoDB.Driver.Tests
{
    public class ListDatabasesTests : IDisposable
    {
        private string _databaseName = $"authorizedDatabases{Guid.NewGuid()}";
        private string _userName = $"authorizedDatabases{Guid.NewGuid()}";
        private string _password = "authorizedDatabases";

        public ListDatabasesTests()
        {
            var client = DriverTestConfiguration.Client;
            CreateListDatabasesRole(client);
            client.GetDatabase(_databaseName).GetCollection<BsonDocument>("test").InsertOne(new BsonDocument());
            CreateListDatabasesUser(client, _userName, _password, _databaseName);
        }

        public void Dispose()
        {
            var client = DriverTestConfiguration.Client;
            DropListDatabasesUser(client, _userName);
            client.DropDatabase(_databaseName);
            DropListDatabasesRole(client);
        }

        [SkippableTheory]
        [ParameterAttributeData]
        public void Execute_should_return_the_expected_result_when_AuthorizedDatabases_is_used(
            [Values(false, true)] bool authorizedDatabases)
        {
            RequireServer.Check().Supports(Feature.ListDatabasesAuthorizedDatabases).Authentication(true);

            var settings = DriverTestConfiguration.Client.Settings.Clone();
            settings.Credential = MongoCredential.FromComponents(mechanism: null, source: null, username: _userName, password: _password);

            var client = new MongoClient(settings);

            var options = new ListDatabasesOptions
            {
                AuthorizedDatabases = authorizedDatabases,
                NameOnly = true,
            };

            var result = client.ListDatabases(options);

            var databases = ReadCursorToEnd(result);
            if (authorizedDatabases)
            {
                databases.Should().BeEquivalentTo(new BsonArray { new BsonDocument { { "name", _databaseName } } });
            }
            else
            {
                databases.Count.Should().BeGreaterThan(1);
            }
        }

        private void CreateListDatabasesRole(MongoClient mongoClient)
        {
            var priviliges = new BsonArray
            {
                new BsonDocument { { "resource", new BsonDocument { { "cluster", true } } }, { "actions", new BsonArray { "listDatabases" } } },
            };
            var command = new BsonDocument
            {
                { "createRole", "listDatabases" },
                { "privileges", priviliges },
                { "roles", new BsonArray() },
            };

            mongoClient.GetDatabase("admin").RunCommand<BsonDocument>(command);
        }

        private void CreateListDatabasesUser(MongoClient mongoClient, string username, string password, string databaseName)
        {
            var roles = new BsonArray
            {
                new BsonDocument { { "role", "read" }, { "db", databaseName } },
                new BsonDocument { { "role", "listDatabases" }, { "db", "admin" } },
            };
            var command = new BsonDocument
            {
                { "createUser", username },
                { "pwd", password },
                { "roles", roles },
            };

            mongoClient.GetDatabase("admin").RunCommand<BsonDocument>(command);
        }

        private void DropListDatabasesRole(MongoClient mongoClient)
        {
            var command = new BsonDocument
            {
                { "dropRole", "listDatabases" }
            };

            mongoClient.GetDatabase("admin").RunCommand<BsonDocument>(command);
        }

        private void DropListDatabasesUser(MongoClient mongoClient, string username)
        {
            var command = new BsonDocument { { "dropUser", username } };

            mongoClient.GetDatabase("admin").RunCommand<BsonDocument>(command);
        }

        private List<T> ReadCursorToEnd<T>(IAsyncCursor<T> cursor)
        {
            var documents = new List<T>();
            while (cursor.MoveNext(CancellationToken.None))
            {
                foreach (var document in cursor.Current)
                {
                    documents.Add(document);
                }
            }
            return documents;
        }
    }
}
