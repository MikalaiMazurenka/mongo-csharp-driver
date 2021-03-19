/* Copyright 2010-present MongoDB Inc.
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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace MongoDB.Driver.TestConsoleApplication
{
    class Program
    {
        private static readonly FacilityAttributeStringValue Country = new()
        {
            Id = Guid.NewGuid(),
            AttributeId = Guid.NewGuid(),
            Value = "Spain"
        };

        static async Task Main(string[] args)
        {
            BsonClassMap.RegisterClassMap<FacilityAttributeDateTimeValue>();
            BsonClassMap.RegisterClassMap<FacilityAttributeStringValue>();
            BsonClassMap.RegisterClassMap<FacilityAttributeDecimalValue>();
            BsonClassMap.RegisterClassMap<MonitoredDataSourceId>();
            BsonClassMap.RegisterClassMap<CustomDataSourceId>();

            var client = new MongoClient("mongodb://localhost:27017/?readPreference=primary&appname=MongoDB.Driver.TestConsoleApplication-vscode%200.4.1&ssl=false");

            await client.DropDatabaseAsync("mongo-benchmark");
            var db = client.GetDatabase("mongo-benchmark");
            var typedCollection = db.GetCollection<Facility>("facilities");

            //Insert 5000 facilities with 150 parameter and 500 attributes each
            await typedCollection.InsertManyAsync(GetFacilities());
            var rawCollection = db.GetCollection<RawBsonDocument>("facilities");

            // Untyped
            var sw = Stopwatch.StartNew();
            var rawValues = await rawCollection.Find(new BsonDocument()).ToListAsync();
            sw.Stop();

            Console.WriteLine($"{rawValues.Count} raw values took {sw.ElapsedMilliseconds} milliseconds");

            //Typed
            sw.Restart();
            var typedValues = await typedCollection.Find(new BsonDocument()).ToListAsync();
            sw.Stop();

            Console.WriteLine($"{typedValues.Count} typed values took {sw.ElapsedMilliseconds} milliseconds");
        }

        private static IEnumerable<Facility> GetFacilities()
        {
            var parameter = Enumerable.Range(0, 150).Select(_ => Guid.NewGuid()).ToList();
            var attributes = Enumerable.Range(0, 500).Select(_ => Guid.NewGuid()).ToList();

            for (var i = 0; i < 5000; i++)
            {
                yield return new Facility
                {
                    Id = Guid.NewGuid(),
                    ParametersValues =
                        parameter
                            .Select(p => new ParameterValue
                            {
                                Id = Guid.NewGuid(),
                                ParameterId = p,
                                DataSourceId = new CustomDataSourceId { Id = Guid.NewGuid() }
                            })
                            .ToList(),
                    AttributesValues = GetAttributeValues(i)
                };
            }

            List<AttributeValue> GetAttributeValues(int i)
            {
                if (i % 3 == 0)
                {
                    return new List<AttributeValue>(
                        attributes
                            .Take(499)
                            .Select(a => new FacilityAttributeStringValue
                            {
                                Id = Guid.NewGuid(),
                                AttributeId = a,
                                Value = a.ToString()
                            })
                            .Append(Country));
                }
                else
                {
                    return new List<AttributeValue>(
                        attributes
                            .Select(a => new FacilityAttributeStringValue
                            {
                                Id = Guid.NewGuid(),
                                AttributeId = a,
                                Value = a.ToString()
                            }));
                }
            }
        }
    }
}
