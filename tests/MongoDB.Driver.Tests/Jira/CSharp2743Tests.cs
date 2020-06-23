using System.Collections.Generic;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization.Attributes;
using Xunit;
using Xunit.Abstractions;

namespace MongoDB.Driver.Tests.Jira
{
    public class CSharp2743Tests
    {
        private readonly string _databaseName;
        private readonly string _collectionName;
        private readonly ITestOutputHelper _output;
        private readonly IMongoClient _client;
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<Employee> _collection;


        public CSharp2743Tests(ITestOutputHelper output)
        {
            _databaseName = CoreTestConfiguration.DatabaseNamespace.DatabaseName;
            _collectionName = "employees";
            _output = output;
            _client = DriverTestConfiguration.Client;
            _database = _client.GetDatabase(_databaseName);
            _collection = _database.GetCollection<Employee>(_collectionName);
            EnsureTestData(_database, _collectionName);
        }

        [Fact]
        public void Test()
        {
            var jsonWriterSettings = new JsonWriterSettings();

            var documentationQuery = _collection
                .Aggregate()
                .GraphLookup(
                    from: _collection,
                    connectFromField: x => x.ReportsTo,
                    connectToField: x => x.Ids,
                    startWith: x => x.ReportsTo,
                    @as: (EmployeeWithReportingHierarchy x) => x.ReportingHierarchy);
            _output.WriteLine("Documentation query:\n" + documentationQuery);

            var documentationResults = documentationQuery.ToList().ToJson();
            _output.WriteLine("Documentation results:\n" + documentationResults);


            var userQuery = _collection
                .Aggregate()
                .GraphLookup(
                    from: _collection,
                    connectFromField: x => x.Ids,
                    connectToField: x => x.ReportsTo,
                    startWith: x => x.Ids,
                    @as: (EmployeeWithReportingHierarchy x) => x.ReportingHierarchy);
            _output.WriteLine("User query:\n" + userQuery);

            var userResults = userQuery.ToList().ToJson();
            _output.WriteLine("User results:\n" + userResults);
        }

        private void EnsureTestData(IMongoDatabase database, string collectionName)
        {
            database.DropCollection(collectionName);
            var collection = database.GetCollection<Employee>(collectionName);
            var documents = new Employee[]
            {
                new Employee { Id = 1, Ids = new Identifier[] { new StringIdentifier { Id = "Dev" } } },
                new Employee { Id = 2, Ids = new Identifier[] { new IntIdentifier { Id = 2 } }, ReportsTo = new List<Identifier> { new StringIdentifier { Id = "Dev" } } },
                new Employee { Id = 3, Ids = new Identifier[] { new StringIdentifier { Id = "Ron" } }, ReportsTo = new List<Identifier> { new StringIdentifier { Id = "Eliot" } } },
                new Employee { Id = 4, Ids = new Identifier[] { new StringIdentifier { Id = "Andrew" } }, ReportsTo = new List<Identifier> { new StringIdentifier { Id = "Eliot" } } },
                new Employee { Id = 5, Ids = new Identifier[] { new StringIdentifier { Id = "Asya" } }, ReportsTo = new List<Identifier> { new StringIdentifier { Id = "Ron" } } },
                new Employee { Id = 6, Ids = new Identifier[] { new StringIdentifier { Id = "Dan" } }, ReportsTo = new List<Identifier> { new StringIdentifier { Id = "Andrew" } } },
            };
            collection.InsertMany(documents);
        }

        public class Identifier
        {
        }

        public class IntIdentifier : Identifier
        {
            public int Id;
        }

        public class StringIdentifier : Identifier
        {
            public string Id;
        }

        public class Employee
        {
            [BsonId]
            public int Id;
            public Identifier[] Ids;
            public List<Identifier> ReportsTo;
        }

        public class EmployeeWithReportingHierarchy
        {
            [BsonId]
            public int Id;
            public Identifier[] Ids;
            public List<Identifier> ReportsTo;
            public List<Employee> ReportingHierarchy;
        }
    }
}
