/* Copyright 2020–present MongoDB Inc.
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
using System.Net.Http;
using System.Security;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Misc;

namespace MongoDB.Driver.Core.Authentication
{
    /// <summary>
    /// The Mongo AWS authenticator.
    /// </summary>
    public class MongoAWSAuthenticator : SaslAuthenticator
    {
        private static readonly Lazy<HttpClient> _httpClientInstance = new Lazy<HttpClient>(() =>
        {
            var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            return httpClient;
        });

        // constants
        private const string awsContainerCredentialsRelativeUriKey = "AWS_CONTAINER_CREDENTIALS_RELATIVE_URI";
        private const string awsSessionTokenMechanismPropertyKey = "AWS_SESSION_TOKEN";
        private const string ecsBaseUri = "http://169.254.170.2";
        private const string ec2BaseUri = "http://169.254.169.254";
        private const int randomLength = 32;

        // static properties
        /// <summary>
        /// Gets the name of the mechanism.
        /// </summary>
        /// <value>
        /// The name of the mechanism.
        /// </value>
        public static string MechanismName
        {
            get { return "MONGODB-AWS"; }
        }

        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="GssapiAuthenticator"/> class.
        /// </summary>
        /// <param name="credential">The credentials.</param>
        /// <param name="properties">The properties.</param>
        public MongoAWSAuthenticator(UsernamePasswordCredential credential, IEnumerable<KeyValuePair<string, string>> properties)
            : base(CreateMechanism(credential, properties, new DefaultRandomByteGenerator()))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GssapiAuthenticator"/> class.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="properties">The properties.</param>
        public MongoAWSAuthenticator(string username, IEnumerable<KeyValuePair<string, string>> properties)
            : base(CreateMechanism(username, null, properties, new DefaultRandomByteGenerator()))
        {
        }

        /// <inheritdoc/>
        public override string DatabaseName
        {
            get { return "$external"; }
        }

        private static MongoAWSMechanism CreateMechanism(
            UsernamePasswordCredential credential,
            IEnumerable<KeyValuePair<string, string>> properties,
            IRandomByteGenerator randomByteGenerator)
        {
            if (credential.Source != "$external")
            {
                throw new ArgumentException("MONGO AWS authentication may only use the $external source.", "credential");
            }

            return CreateMechanism(credential.Username, credential.Password, properties, randomByteGenerator);
        }

        private static MongoAWSMechanism CreateMechanism(
            string username,
            SecureString securePassword,
            IEnumerable<KeyValuePair<string, string>> properties,
            IRandomByteGenerator randomByteGenerator)
        {
            var relativeUri = Environment.GetEnvironmentVariable(awsContainerCredentialsRelativeUriKey);
            var methodsList = new Func<AwsCredentials>[]
            {
                () => AcquireCredentialsFromMongoCredentials(username, securePassword, properties),
                () => AcquireMechanismDataFromEnvironmentVariables(),
                () => AcquireMechanismDataFromEcsResponse(ecsBaseUri, relativeUri),
                () => AcquireMechanismDataFromEc2Response(ec2BaseUri)
            };

            foreach (var method in methodsList)
            {
                var creds = method();
                if (creds != null)
                {
                    return new MongoAWSMechanism(creds.Credentials, creds.SessionToken, randomByteGenerator);
                }
            }

            throw new ArgumentException("A MONGODB-AWS must have access key ID.");
        }

        private static AwsCredentials AcquireCredentialsFromMongoCredentials(string username, SecureString securePassword, IEnumerable<KeyValuePair<string, string>> properties)
        {
            var sessionToken = ExtractSessionTokenFromMechanismProperties(properties);
            ValidateCredentials(username, securePassword, sessionToken);

            if (username == null)
            {
                return null;
            }

            return new AwsCredentials
            {
                Credentials = new UsernamePasswordCredential("$external", username, securePassword),
                SessionToken = sessionToken
            };
        }

        private static AwsCredentials AcquireMechanismDataFromEnvironmentVariables()
        {
            var username = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            var password = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
            var sessionToken = Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN");
            ValidateCredentials(username, password, sessionToken);

            if (username == null)
            {
                return null;
            }

            return new AwsCredentials
            {
                Credentials = new UsernamePasswordCredential("$external", username, password),
                SessionToken = sessionToken
            };
        }

        private static AwsCredentials AcquireMechanismDataFromEcsResponse(string baseUri, string relativeUri)
        {
            if (relativeUri == null)
            {
                return null;
            }

            var response = GetECSResponse(new Uri(baseUri), relativeUri).GetAwaiter().GetResult();
            var parsedReponse = BsonDocument.Parse(response);
            var username = parsedReponse.GetValue("AccessKeyId").AsString;
            var password = parsedReponse.GetValue("SecretAccessKey").AsString;
            var sessionToken = parsedReponse.GetValue("Token").AsString;
            ValidateCredentials(username, password, sessionToken);

            if (username == null)
            {
                return null;
            }

            return new AwsCredentials
            {
                Credentials = new UsernamePasswordCredential("$external", username, password),
                SessionToken = sessionToken
            };
        }

        private static AwsCredentials AcquireMechanismDataFromEc2Response(string uri)
        {
            var response = GetEC2Response(new Uri(uri)).GetAwaiter().GetResult();
            var parsedReponse = BsonDocument.Parse(response);
            var username = parsedReponse.GetValue("AccessKeyId").AsString;
            var password = parsedReponse.GetValue("SecretAccessKey").AsString;
            var sessionToken = parsedReponse.GetValue("Token").AsString;
            ValidateCredentials(username, password, sessionToken);

            if (username == null)
            {
                return null;
            }

            return new AwsCredentials
            {
                Credentials = new UsernamePasswordCredential("$external", username, password),
                SessionToken = sessionToken
            };
        }

        private static string ExtractSessionTokenFromMechanismProperties(IEnumerable<KeyValuePair<string, string>> properties)
        {
            string sessionToken = null;
            if (properties != null)
            {
                foreach (var pair in properties)
                {
                    switch (pair.Key.ToUpperInvariant())
                    {
                        case awsSessionTokenMechanismPropertyKey:
                            sessionToken = (string)pair.Value;
                            break;
                        default:
                            throw new ArgumentException($"Unknown AWS property '{pair.Key}'.", "properties");
                    }
                }
            }

            return sessionToken;
        }

        private static async Task<string> GetEC2Response(Uri baseUri)
        {
            // Acquire token

            var tokenRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(baseUri, "latest/api/token"),
                Method = HttpMethod.Put,
            };
            tokenRequest.Headers.Add("X-aws-ec2-metadata-token-ttl-seconds", "30");
            var token = await GetHttpContent(tokenRequest, "Failed to acquire EC2 token.").ConfigureAwait(false);

            // Acquire role name

            var roleRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(baseUri, "latest/meta-data/iam/security-credentials/"),
                Method = HttpMethod.Get
            };
            roleRequest.Headers.Add("X-aws-ec2-metadata-token", token);
            var roleName = await GetHttpContent(roleRequest, "Failed to acquire EC2 role name.").ConfigureAwait(false);

            // Acquire credentials

            var credentialsUri = new Uri(baseUri, "latest/meta-data/iam/security-credentials/");
            var credentialsRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(credentialsUri, roleName),
                Method = HttpMethod.Get
            };
            credentialsRequest.Headers.Add("X-aws-ec2-metadata-token", token);
            var credentials = await GetHttpContent(credentialsRequest, "Failed to acquire EC2 credentials.").ConfigureAwait(false);

            return credentials;
        }

        private static async Task<string> GetECSResponse(Uri baseUri, string relativeUri)
        {
            var credentialsRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(baseUri, relativeUri),
                Method = HttpMethod.Get
            };

            return await GetHttpContent(credentialsRequest, "Failed to acquire ECS credentials.").ConfigureAwait(false);
        }

        private static async Task<string> GetHttpContent(HttpRequestMessage request, string exceptionMessage)
        {
            HttpResponseMessage response;
            try
            {
                response = await _httpClientInstance.Value.SendAsync(request).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                throw new MongoClientException(exceptionMessage, ex);
            }
            catch (HttpRequestException ex)
            {
                throw new MongoClientException(exceptionMessage, ex);
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new MongoClientException(exceptionMessage);
            }

            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private static void ValidateCredentials(string username, string password, string sessionToken)
        {
            if (username == null && (password != null || sessionToken != null))
            {
                throw new ArgumentException("A MONGODB-AWS must have access key id.");
            }
            if (username != null && password == null)
            {
                throw new ArgumentException("A MONGODB-AWS must have secret access key.");
            }
        }

        private static void ValidateCredentials(string username, SecureString password, string sessionToken)
        {
            if (username == null && (password != null || sessionToken != null))
            {
                throw new ArgumentException("A MONGODB-AWS must have access key id.");
            }
            if (username != null && password == null)
            {
                throw new ArgumentException("A MONGODB-AWS must have secret access key.");
            }
        }

        // nested classes
        private class AwsCredentials
        {
            public UsernamePasswordCredential Credentials;
            public string SessionToken;
        }

        private class MongoAWSMechanism : ISaslMechanism
        {
            private readonly UsernamePasswordCredential _credential;
            private readonly string _name;
            private readonly IRandomByteGenerator _randomByteGenerator;
            private readonly string _sessionToken;

            public MongoAWSMechanism(
                UsernamePasswordCredential credential,
                string sessionToken,
                IRandomByteGenerator randomByteGenerator)
            {
                _name = "MONGODB-AWS";
                _credential = Ensure.IsNotNull(credential, nameof(credential));
                _sessionToken = sessionToken;
                _randomByteGenerator = Ensure.IsNotNull(randomByteGenerator, nameof(randomByteGenerator));
            }

            public string Name
            {
                get { return _name; }
            }

            public ISaslStep Initialize(IConnection connection, SaslConversation conversation, ConnectionDescription description)
            {
                Ensure.IsNotNull(connection, nameof(connection));
                Ensure.IsNotNull(description, nameof(description));

                var nonce = GenerateRandomBytes();

                var document = new BsonDocument()
                    .Add("r", new BsonBinaryData(nonce))
                    .Add("p", new BsonInt32('n'));

                var clientFirstMessageBytes = ToBytes(document);

                return new ClientFirst(clientFirstMessageBytes, nonce, _credential, _sessionToken);
            }

            private byte[] GenerateRandomBytes()
            {
                return _randomByteGenerator.Generate(randomLength);
            }
        }

        private static BsonDocument ToDocument(byte[] bytes)
        {
            MemoryStream stream = new MemoryStream(bytes);
            using (var jsonReader = new BsonBinaryReader(stream))
            {
                var context = BsonDeserializationContext.CreateRoot(jsonReader);
                return BsonDocumentSerializer.Instance.Deserialize(context);
            }
        }

        private static byte[] ToBytes(BsonDocument doc)
        {
            BsonBinaryWriterSettings settings = new BsonBinaryWriterSettings()
            {
#pragma warning disable 618
                GuidRepresentation = GuidRepresentation.Standard
#pragma warning restore 618
            };
            return doc.ToBson(null, settings);
        }

        private class ClientFirst : ISaslStep
        {
            private readonly byte[] _bytesToSendToServer;
            private readonly byte[] _nonce;
            private readonly UsernamePasswordCredential _credential;
            private readonly string _sessionToken;

            public ClientFirst(
                byte[] bytesToSendToServer,
                byte[] nonce,
                UsernamePasswordCredential credential,
                string sessionToken)
            {
                _bytesToSendToServer = bytesToSendToServer;
                _nonce = nonce;
                _credential = credential;
                _sessionToken = sessionToken;
            }

            public byte[] BytesToSendToServer
            {
                get { return _bytesToSendToServer; }
            }

            public bool IsComplete
            {
                get { return false; }
            }

            public ISaslStep Transition(SaslConversation conversation, byte[] bytesReceivedFromServer)
            {
                var serverFirstMessageDoc = ToDocument(bytesReceivedFromServer);
                var serverNonce = serverFirstMessageDoc["s"].AsByteArray;
                var host = serverFirstMessageDoc["h"].AsString;

                if (serverNonce.Length != randomLength * 2 || !serverNonce.Take(randomLength).SequenceEqual(_nonce))
                {
                    throw new MongoAuthenticationException(conversation.ConnectionId, message: "Server sent an invalid nonce.");
                }

                var tuple = AwsSignatureVersion4.SignRequest(
                    _credential.Username,
                    _credential.GetInsecurePassword(),
                    _sessionToken,
                    serverNonce,
                    host);

                var document = new BsonDocument()
                    .Add("a", tuple.Item1)
                    .Add("d", tuple.Item2);

                if (_sessionToken != null)
                {
                    document.Add("t", _sessionToken);
                }

                var clientSecondMessageBytes = ToBytes(document);

                return new ClientLast(clientSecondMessageBytes);
            }
        }

        private class ClientLast : ISaslStep
        {
            private readonly byte[] _bytesToSendToServer;

            public ClientLast(byte[] bytesToSendToServer)
            {
                _bytesToSendToServer = bytesToSendToServer;
            }

            public byte[] BytesToSendToServer
            {
                get { return _bytesToSendToServer; }
            }

            public bool IsComplete
            {
                get { return false; }
            }

            public ISaslStep Transition(SaslConversation conversation, byte[] bytesReceivedFromServer)
            {
                return new CompletedStep();
            }
        }
    }
}
