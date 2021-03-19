using System;
using MongoDB.Driver.TestConsoleApplication;

namespace MongoDB.Driver.TestConsoleApplication
{
    public class ParameterValue
	{
		public Guid Id { get; set; }
		public Guid ParameterId { get; set; }
		public DataSourceId DataSourceId { get; set; }
	}
}