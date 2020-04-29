/* Copyright 2020 MongoDB Inc.
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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.TestHelpers.JsonDrivenTests;
using Xunit;

namespace MongoDB.Bson.Tests.Specifications.bson_corpus
{
    public class BsonCorpusTestRunner
    {
        [SkippableTheory]
        [ClassData(typeof(TestCaseFactory))]
        public void RunTestDefinition(JsonDrivenTestCase testCase)
        {
            var definition = testCase.Test;
            var testType = definition["type"].AsString;
            var test = testCase.Test;

            switch (testType)
            {
                case "valid": RunValidTest(test); break;
                case "decodeErrors": RunDecodeErrorsTest(test); break;
                case "parseErrors": RunParseErrorsTest(test); break;
                default: throw new Exception($"Invalid test type: {testType}.");
            }
        }

        // private methods
        private BsonDocument DecodeBson(byte[] bytes)
        {
#pragma warning disable 618
            var readerSettings = new BsonBinaryReaderSettings
            {
                FixOldBinarySubTypeOnInput = false,
                FixOldDateTimeMaxValueOnInput = false
            };
            if (BsonDefaults.GuidRepresentationMode == GuidRepresentationMode.V2)
            {
                readerSettings.GuidRepresentation = GuidRepresentation.Unspecified;
            }
#pragma warning restore 618
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BsonBinaryReader(stream, readerSettings))
            {
                var context = BsonDeserializationContext.CreateRoot(reader);
                return BsonDocumentSerializer.Instance.Deserialize(context);
            }
        }

        private BsonDocument DecodeExtendedJson(string extendedJson)
        {
            return BsonDocument.Parse(extendedJson);
        }

        private byte[] EncodeBson(BsonDocument document)
        {
#pragma warning disable 618
            var writerSettings = new BsonBinaryWriterSettings
            {
                FixOldBinarySubTypeOnOutput = false,
            };
            if (BsonDefaults.GuidRepresentationMode == GuidRepresentationMode.V2)
            {
                writerSettings.GuidRepresentation = GuidRepresentation.Unspecified;
            }
#pragma warning restore 618
            using (var stream = new MemoryStream())
            using (var writer = new BsonBinaryWriter(stream, writerSettings))
            {
                var context = BsonSerializationContext.CreateRoot(writer);
                BsonDocumentSerializer.Instance.Serialize(context, document);
                return stream.ToArray();
            }
        }

        private string EncodeCanonicalExtendedJson(BsonDocument document)
        {
#pragma warning disable 618
            var writerSettings = new JsonWriterSettings
            {
                OutputMode = JsonOutputMode.CanonicalExtendedJson,
            };
            if (BsonDefaults.GuidRepresentationMode == GuidRepresentationMode.V2)
            {
                writerSettings.GuidRepresentation = GuidRepresentation.Unspecified;
            }
#pragma warning restore 618
            var json = document.ToJson(writerSettings);
            return json.Replace(" ", "");
        }

        private string EncodeRelaxedExtendedJson(BsonDocument document)
        {
#pragma warning disable 618
            var writerSettings = new JsonWriterSettings
            {
                OutputMode = JsonOutputMode.RelaxedExtendedJson,
            };
            if (BsonDefaults.GuidRepresentationMode == GuidRepresentationMode.V2)
            {
                writerSettings.GuidRepresentation = GuidRepresentation.Unspecified;
            }
#pragma warning restore 618
            var json = document.ToJson(writerSettings);
            return json.Replace(" ", "");
        }

        private void RunDecodeErrorsTest(BsonDocument test)
        {
            JsonDrivenHelper.EnsureAllFieldsAreValid(test, "type", "description", "bson");

            var bson = BsonUtils.ParseHexString(test["bson"].AsString);

            var exception = Record.Exception(() =>
            {
                using (var stream = new MemoryStream(bson))
                using (var reader = new BsonBinaryReader(stream))
                {
                    while (!reader.IsAtEndOfFile())
                    {
                        _ = BsonSerializer.Deserialize<BsonDocument>(reader);
                    }
                }
            });

            exception.Should().NotBeNull();
        }

        private void RunParseErrorsTest(BsonDocument test)
        {
            JsonDrivenHelper.EnsureAllFieldsAreValid(test, "type", "description", "string");

            var json = test["string"].AsString;

            var exception = Record.Exception(() => BsonDocument.Parse(json));

            exception.Should().NotBeNull();
        }

        private void RunValidTest(BsonDocument test)
        {
            JsonDrivenHelper.EnsureAllFieldsAreValid(
                test,
                "type",
                "description",
                "canonical_bson",
                "canonical_extjson",
                "relaxed_extjson",
                "degenerate_bson",
                "degenerate_extjson",
                "converted_bson",
                "converted_extjson",
                "lossy");

            byte[] cB = null;
            if (test.Contains("canonical_bson"))
            {
                cB = BsonUtils.ParseHexString(test["canonical_bson"].AsString);
            }

            byte[] dB = null;
            if (test.Contains("degenerate_bson"))
            {
                dB = BsonUtils.ParseHexString(test["degenerate_bson"].AsString);
            }

            string cEJ = null;
            if (test.Contains("canonical_extjson"))
            {
                cEJ = test["canonical_extjson"].AsString.Replace(" ", "");
                cEJ = UnescapeUnicodeCharacters(cEJ);
            }

            string dEJ = null;
            if (test.Contains("degenerate_extjson"))
            {
                dEJ = test["degenerate_extjson"].AsString.Replace(" ", "");
                dEJ = UnescapeUnicodeCharacters(dEJ);
            }

            string rEJ = null;
            if (test.Contains("relaxed_extjson"))
            {
                rEJ = test["relaxed_extjson"].AsString.Replace(" ", "");
                rEJ = UnescapeUnicodeCharacters(rEJ);
            }

            if (cB != null)
            {
                EncodeBson(DecodeBson(cB)).Should().Equal(cB, "native_to_bson( bson_to_native(cB) ) = cB");
                EncodeCanonicalExtendedJson(DecodeBson(cB)).Should().Be(cEJ, "native_to_canonical_extended_json( bson_to_native(cB) ) = cEJ");
                if (rEJ != null)
                {
                    EncodeRelaxedExtendedJson(DecodeBson(cB)).Should().Be(rEJ, "native_to_relaxed_extended_json( bson_to_native(cB) ) = rEJ");
                }
            }

            if (cEJ != null)
            {
                EncodeCanonicalExtendedJson(DecodeExtendedJson(cEJ)).Should().Be(cEJ, "native_to_canonical_extended_json( json_to_native(cEJ) ) = cEJ");
                if (!test.GetValue("lossy", false).ToBoolean())
                {
                    EncodeBson(DecodeExtendedJson(cEJ)).Should().Equal(cB, "native_to_bson( json_to_native(cEJ) ) = cB");
                }
            }

            if (dB != null)
            {
                EncodeBson(DecodeBson(dB)).Should().Equal(cB, "native_to_bson( bson_to_native(dB) ) = cB");
            }

            if (dEJ != null)
            {
                EncodeCanonicalExtendedJson(DecodeExtendedJson(dEJ)).Should().Be(cEJ, "native_to_canonical_extended_json( json_to_native(dEJ) ) = cEJ");
                if (!test.GetValue("lossy", false).ToBoolean())
                {
                    EncodeBson(DecodeExtendedJson(dEJ)).Should().Equal(cB, "native_to_bson( json_to_native(dEJ) ) = cB");
                }
            }

            if (rEJ != null)
            {
                EncodeRelaxedExtendedJson(DecodeExtendedJson(rEJ)).Should().Be(rEJ, "native_to_relaxed_extended_json( json_to_native(rEJ) ) = rEJ");
            }
        }

        private string UnescapeUnicodeCharacters(string value)
        {
            var pattern = @"\\u[0-9a-fA-F]{4}";
            var unescaped = Regex.Replace(value, pattern, match =>
            {
                var bytes = BsonUtils.ParseHexString(match.Value.Substring(2, 4));
                var c = (char)(bytes[0] << 8 | bytes[1]);
                return c < 0x20 ? match.Value : new string(c, 1);
            });
            return unescaped;
        }

        // nested types
        private class TestCaseFactory : JsonDrivenTestCaseFactory
        {
            #region static
            private static readonly string[] __ignoredTestNames =
            {
                "dbpointer.json", // dbpointer not supported
                "Bad DBpointer (extra field)", // dbpointer not supported
                "double.json:-0.0", // minus zero equals zero in .NET
                "All BSON types", // dbpointer not supported
                "Bad $date (number, not string or hash)", // requires breaking change
                "Bad $numberDecimal (number, not string)", // requires breaking change
                "Bad $numberDouble (number, not string)", // requires breaking change
                "Bad $numberInt (number, not string)", // requires breaking change
                "Bad $numberLong (number, not string)", // requires breaking change
                "Bad $timestamp (type is number, not doc)", // requires breaking change
                "Bad DBRef (ref is number, not string)", // requires breaking change
                "Bad DBRef (db is number, not string)", // requires breaking change
            };
            #endregion

            // protected properties
            protected override string PathPrefix => "MongoDB.Bson.Tests.Specifications.bson_corpus.tests.";

            // protected methods
            protected override IEnumerable<JsonDrivenTestCase> CreateTestCases(BsonDocument document)
            {
                var shared = document;

                var tests = CreateTestCase(shared, "valid")
                    .Concat(CreateTestCase(shared, "decodeErrors"))
                    .Concat(CreateTestCase(shared, "parseErrors"));

                return tests.Where(t => !__ignoredTestNames.Any(ignoredName => t.Name.Contains(ignoredName)));
            }

            // private methods
            private IEnumerable<JsonDrivenTestCase> CreateTestCase(BsonDocument shared, string testType)
            {
                if (shared.Contains(testType))
                {
                    var tests = shared[testType].AsBsonArray.Select(item => item.AsBsonDocument).ToList();
                    for (var i = 0; i < tests.Count; i++)
                    {
                        var test = tests[i];
                        var name = GetTestCaseName(shared, test, i);
                        var enrichedName = $"{name}:type={testType}";
                        var enrichedTest = test.DeepClone().AsBsonDocument.Add("type", testType);
                        yield return new JsonDrivenTestCase(enrichedName, shared, enrichedTest);
                    }
                }

                yield break;
            }
        }
    }
}
