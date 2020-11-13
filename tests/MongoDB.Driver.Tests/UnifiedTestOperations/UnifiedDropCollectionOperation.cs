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
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver.Tests.Specifications.unified_test_format;

namespace MongoDB.Driver.Tests.UnifiedTestOperations
{
    public class UnifiedDropCollectionOperation : IUnifiedEntityTestOperation
    {
        private readonly string _collectionName;
        private readonly IMongoDatabase _database;

        public UnifiedDropCollectionOperation(
            IMongoDatabase database,
            string collectionName)
        {
            _database = database;
            _collectionName = collectionName;
        }

        public OperationResult Execute(CancellationToken cancellationToken)
        {
            try
            {
                _database.DropCollection(_collectionName);

                return OperationResult.FromResult(null);
            }
            catch (Exception exception)
            {
                return OperationResult.FromException(exception);
            }
        }

        public async Task<OperationResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _database.DropCollectionAsync(_collectionName);

                return OperationResult.FromResult(null);
            }
            catch (Exception exception)
            {
                return OperationResult.FromException(exception);
            }
        }
    }

    public class UnifiedDropCollectionOperationBuilder
    {
        private readonly EntityMap _entityMap;

        public UnifiedDropCollectionOperationBuilder(EntityMap entityMap)
        {
            _entityMap = entityMap;
        }

        public UnifiedDropCollectionOperation Build(string targetDatabaseId, BsonDocument arguments)
        {
            var database = _entityMap.GetDatabase(targetDatabaseId);

            string collectionName = null;

            foreach (var argument in arguments)
            {
                switch (argument.Name)
                {
                    case "collection":
                        collectionName = argument.Value.AsString;
                        break;
                    default:
                        throw new FormatException($"Invalid DropCollectionOperation argument name: '{argument.Name}'");
                }
            }

            return new UnifiedDropCollectionOperation(database, collectionName);
        }
    }
}
