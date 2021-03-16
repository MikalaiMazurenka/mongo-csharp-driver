### 4.4 server compatibility issues

1. 🦖 `MapReduce` operation has several properties inaccessible: `Duration`, `EmitCount`, `OutputCount`, `InputCount`. Non-legacy part of the driver has an up-to-date API which functions correctly.
2. `AggregateExplain` operation structure changed. No big deal, since it is intended to be read by humans. See respective ticket: [CSHARP-2842: The format of Aggregate Explain response has been changed](https://jira.mongodb.org/browse/CSHARP-2842)
3. 🦖 `GridFS.Download` not working on sharded cluster configuration. See respective ticket: [CSHARP-3423: Fix CSharp269Tests.TestUploadAndDownload GridFs test on sharded cluster](https://jira.mongodb.org/browse/CSHARP-3423)

### latest server compatibility issues

4. 🦖 `GeoSearch` and `GeoHaystack` operations were removed from 4.9.0 server. These operations are from legacy part of the driver and server returns quite comprehensible exception. See respective ticket: [CSHARP-3249: Skip geoSearch and geoHaystack tests when running against server version that doesn't support these commands](https://jira.mongodb.org/browse/CSHARP-3249)
5. `CreateIndexesOperation` result field `expireAfterSeconds` value is rounded since 4.9.0. See respective ticket: [CSHARP-3294: Fix CreateIndexesOperationTests tests on latests](https://jira.mongodb.org/browse/CSHARP-3294)
6. 🦖 `CreateIndex` with `WildcardProjection` reports result `_id` value as boolean (`{ _id : true }`) instead of int (`{ _id : 1 }`) starting from 4.5.0. That's fine, since it is legacy API. See respective ticket: [CSHARP-3078: Fix test CreateIndex_with_wildcardProjection_should_create_expected_index on latest](https://jira.mongodb.org/browse/CSHARP-3078)
7. 🦖 `ReIndex` command fails on replica set configuration, but works on standalone as it used to. This operation is part of legacy API. See respective change in 2.11.0 driver: [CSHARP-3058: Make 'reIndex' a standalone-only command](https://jira.mongodb.org/browse/CSHARP-3058)
8. `ChangeStreamDocument.UpdateDescription` property throws an exception (`System.FormatException : Invalid field name: "truncatedArrays".`) upon accessing since 4.9.0. Other properties work as intended. The issue here is in deserialization of `ChangeStreamUpdateDescription` class. See respective ticket: [CSHARP-3212: Add new field truncatedArrays into cursor.nextBatch.updateDescription](https://jira.mongodb.org/browse/CSHARP-3212)
