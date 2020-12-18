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
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace MongoDB.Driver.TestConsoleApplication
{
    class Program
    {
        static void Main(string[] args)
        {
            var numberOfTasks = 40;
            var tasks = new List<Task>();

            var client = new MongoClient("mongodb://localhost/?maxPoolSize=10"); // "mongodb://localhost/?connect=replicaSet"
            var collection = client.GetDatabase("d").GetCollection<BsonDocument>("c");

            for (int i = 0; i < numberOfTasks; i++)
            {
                var j = i; // Variable capture workaround
                var task = Task.Run(() =>
                {
                    var stopWatch = Stopwatch.StartNew();
                    try
                    {
                        ThreadPool.GetMaxThreads(out var maxThreads, out _);
                        ThreadPool.GetAvailableThreads(out var availableThreads, out _);
                        Console.WriteLine("#{0,-2} [>] Started   | Threads in use: {1}/{2}", j, maxThreads - availableThreads, maxThreads);
                        var count = collection.CountDocuments(new BsonDocument());
                        Console.WriteLine("#{0,-2} [+] Succeeded | Execution time: {1}", j, stopWatch.ElapsedMilliseconds);
                    }
                    catch (TimeoutException)
                    {
                        Console.WriteLine("#{0,-2} [-] Timed out | Execution time: {1}", j, stopWatch.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                });
                tasks.Add(task);
            }

            Task.WhenAll(tasks).Wait();
            Console.Write("Done. Press return to exit...");
            Console.ReadLine();
        }
    }
}
