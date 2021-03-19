using System;

namespace MongoDB.Driver.TestConsoleApplication
{
    public class CustomDataSourceId : DataSourceId
    {
        public Guid Id { get; set; }
    }
}
