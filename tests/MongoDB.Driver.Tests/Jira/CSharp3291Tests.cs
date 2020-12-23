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

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MongoDB.Bson;
using Xunit;

namespace MongoDB.Driver.Tests.Jira
{
    public class CSharp3291Tests
    {
        [Fact]
        public void Test()
        {
            var routes = new Route[]
            {
                new Route { Stops = new[] { new Stop { StationId = "A" }, new Stop { StationId = "B" }, new Stop { StationId = "C" }, new Stop { StationId = "D" } } },
                new Route { Stops = new[] { new Stop { StationId = "A" }, new Stop { StationId = "B" }, new Stop { StationId = "C" }, new Stop { StationId = "D" } } },
                new Route { Stops = new[] { new Stop { StationId = "A" }, new Stop { StationId = "B" }, new Stop { StationId = "C" }, new Stop { StationId = "D" } } },
                new Route { Stops = new[] { new Stop { StationId = "B" }, new Stop { StationId = "C" }, new Stop { StationId = "D" }, new Stop { StationId = "E" } } }
            };
            var client = DriverTestConfiguration.Client;
            var collection = client
                .GetDatabase(DriverTestConfiguration.DatabaseNamespace.DatabaseName)
                .GetCollection<RouteParent>(DriverTestConfiguration.CollectionNamespace.CollectionName);
            collection.InsertMany(routes);
            var expectedResult = new BsonArray
            {
                new BsonDocument { { "StationId", "B" }, { "TotalStops", 4 } },
                new BsonDocument { { "StationId", "A" }, { "TotalStops", 3 } },
                new BsonDocument { { "StationId", "C" }, { "TotalStops", 4 } },
                new BsonDocument { { "StationId", "E" }, { "TotalStops", 1 } },
                new BsonDocument { { "StationId", "D" }, { "TotalStops", 4 } },
            };

            var result = collection
                .OfType<Route>()
                .Aggregate()
                .Unwind<Route, Stop>(dr => dr.Stops)
                .Group(stop => stop.StationId,
                    g => new
                    {
                        StationId = g.Key,
                        TotalStops = g.Count()
                    }).ToList();

            // Workaround:
            //var result = collection
            //    .OfType<Route>()
            //    .AsQueryable()
            //    .SelectMany(s => s.Stops)
            //    .GroupBy(s => s.StationId)
            //    .Select(g => new { StationId = g.Key, TotalStops = g.Count() })
            //    .ToList();

            new BsonArray(result
                .Select(x => new BsonDocument { { "StationId", x.StationId }, { "TotalStops", x.TotalStops } }))
                .ToList()
                .Should()
                .BeEquivalentTo(expectedResult);
        }

        // nested types
        private class RouteParent
        {
        }

        private class Route : RouteParent
        {
            public IEnumerable<Stop> Stops;
        }

        private class Stop
        {
            public string StationId;
        }
    }
}
