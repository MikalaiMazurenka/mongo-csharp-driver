/* Copyright 2018-present MongoDB Inc.
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.TestHelpers;
using MongoDB.Bson.TestHelpers.XunitExtensions;
using MongoDB.Driver.Core.Authentication;
using MongoDB.Driver.Core.Configuration;
using Xunit;

namespace MongoDB.Driver.Tests.Specifications.auth
{
    public class TestRunner
    {
        [SkippableTheory]
        [ClassData(typeof(TestCaseFactory))]
        public void RunTestDefinition(BsonDocument definition)
        {
            MongoClientSettings mongoClientSettings = null;
            Exception parseException = null;
            try
            {
                //var connectionString = new ConnectionString((string)definition["uri"]);
                //MongoClientSettings.FromConnectionString(connectionString.ToString());
                mongoClientSettings = MongoClientSettings.FromConnectionString((string)definition["uri"]);
            }
            catch (Exception ex)
            {
                parseException = ex;
            }

            if (parseException == null)
            {
                AssertValid(mongoClientSettings, definition);
            }
            else
            {
                AssertInvalid(parseException, definition);
            }
        }

        private void AssertValid(MongoClientSettings mongoClientSettings, BsonDocument definition)
        {
            if (!definition["valid"].ToBoolean())
            {
                throw new AssertionException($"The connection string '{definition["uri"]}' should be invalid.");
            }

            var mongoCredential = mongoClientSettings.Credential;

            var credential = definition["credential"] as BsonDocument;
            if (credential != null)
            {
                mongoCredential.Username.Should().Be(ValueToString(credential["username"]));
#pragma warning disable 618
                mongoCredential.Password.Should().Be(ValueToString(credential["password"]));
#pragma warning restore 618
                mongoCredential.Source.Should().Be(ValueToString(credential["source"]));
                mongoCredential.Mechanism.Should().Be(ValueToString(credential["mechanism"]));

                credential.TryGetValue("mechanism_properties", out var authMechanismOptionsBsonValue);
                var authMechanismOptions = authMechanismOptionsBsonValue as BsonDocument;
                if (authMechanismOptions != null)
                {
                    authMechanismOptions.TryGetValue("CANONICALIZE_HOST_NAME", out var canonicalizeHostNameValue);
                    if (canonicalizeHostNameValue != null)
                    {
                        mongoCredential.ToAuthenticator()._mechanism()._canonicalizeHostName()
                            .Should().Be(canonicalizeHostNameValue.AsBoolean);
                    }

                    authMechanismOptions.TryGetValue("SERVICE_NAME", out var serviceNameValue);
                    if (serviceNameValue != null)
                    {
                        mongoCredential.ToAuthenticator()._mechanism()._serviceName()
                            .Should().Be(ValueToString(serviceNameValue));
                    }
                }
            }
        }

        private void AssertInvalid(Exception ex, BsonDocument definition)
        {
            if (definition["valid"].ToBoolean())
            {
                throw new AssertionException($"The connection string '{definition["uri"]}' should be valid.", ex);
            }
        }

        private string ValueToString(BsonValue value)
        {
            return value == BsonNull.Value ? null : value.ToString();
        }

        private class TestCaseFactory : IEnumerable<object[]>
        {
            // TODO: remove these ignoredTestNames once the driver implements the underlying changes required
            private static readonly string[] __ignoredTestNames =
            {
                //"connection-string: should recognise the mechanism (GSSAPI)"
                //"connection-string: should throw an exception if authSource is invalid (GSSAPI)",
                //"connection-string: should throw an exception if authSource is invalid (MONGODB-X509)",
                //"connection-string: should throw an exception if no username (GSSAPI)",
                //"connection-string: should throw an exception if no username (PLAIN)",
                //"connection-string: should throw an exception if no username (SCRAM-SHA-1)",
                //"connection-string: should throw an exception if no username (SCRAM-SHA-256)",
                //"connection-string: should throw an exception if no username is supplied (MONGODB-CR)",
                //"connection-string: should throw an exception if supplied a password (MONGODB-X509)"
            };

            public IEnumerator<object[]> GetEnumerator()
            {
                const string prefix = "MongoDB.Driver.Tests.Specifications.auth.tests.";
                var executingAssembly = typeof(TestCaseFactory).GetTypeInfo().Assembly;
                var runTestDefinitionParameters = executingAssembly
                    .GetManifestResourceNames()
                    .Where(path => path.StartsWith(prefix) && path.EndsWith(".json"))
                    .Select(path => new { Filename = path.Remove(0, prefix.Length).Remove(path.Length - prefix.Length - 5),
                                          Tests = (BsonArray)ReadDefinition(path)["tests"] })
                    .SelectMany(definition => definition.Tests
                        .Where(test => !__ignoredTestNames.Contains($"{definition.Filename}: {test["description"]}"))
                        .Select(test => new[] { test }));
                return runTestDefinitionParameters.GetEnumerator();
            }
            
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private static BsonDocument ReadDefinition(string path)
            {
                var executingAssembly = typeof(TestCaseFactory).GetTypeInfo().Assembly;
                using (var definitionStream = executingAssembly.GetManifestResourceStream(path))
                using (var definitionStringReader = new StreamReader(definitionStream))
                {
                    var definitionString = definitionStringReader.ReadToEnd();
                    return BsonDocument.Parse(definitionString);
                }
            }
        }
    }

    internal static class SaslMechanismExtensions
    {
        public static bool _canonicalizeHostName(this Object obj) => (bool)Reflector.GetFieldValue(obj, nameof(_canonicalizeHostName));
        public static string _serviceName(this Object obj) => (string)Reflector.GetFieldValue(obj, nameof(_serviceName));
    }

    internal static class AuthenticatorExtensions
    {
        public static Object _mechanism(this IAuthenticator obj) => Reflector.GetFieldValue(obj, nameof(_mechanism));
    }
}
