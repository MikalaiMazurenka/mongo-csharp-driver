### 4.2 server compatibility issues

1. 🦖 `ParallelScan` operation was deprecated in server version 4.1.
See respective ticket: [CSHARP-2329: Skip ParallelScan tests when server version is >= 4.1](https://jira.mongodb.org/browse/CSHARP-2329)

### 4.4 server compatibility issues

2. 🦖 `MapReduce` operation was rewritten in server version 4.4 and the driver version has several properties inaccessible as a result of using an old operation: `Duration`, `EmitCount`, `OutputCount`, `InputCount`. Non-legacy part of the driver has an up-to-date API which functions correctly.
See respective ticket: [CSHARP-2738: Deprecate mapparams, out.sharded, and out.nonAtomic arguments to mapReduce](https://jira.mongodb.org/browse/CSHARP-2738)
3. `IndexInfo.Namespace` property throws exception upon accessing, because `'ns'` field was removed from index definition in server version 4.4.
See respective ticket: [CSHARP-2684: Server latest removed the “ns” field from index definitions](https://jira.mongodb.org/browse/CSHARP-2684)

### latest server compatibility issues
