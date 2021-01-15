/* Copyright 2013-present MongoDB Inc.
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
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Core.WireProtocol;

namespace MongoDB.Driver.Core.Authentication
{
    /// <summary>
    /// A MONGODB-CR authenticator.
    /// This authenticator was replaced by <see cref="ScramSha1Authenticator"/> in MongoDB 3.0, and is now deprecated.
    /// </summary>
    [Obsolete("This authenticator was replaced by ScramSha1Authenticator in MongoDB 3.0, and is now deprecated.")]
    public sealed class MongoDBCRAuthenticator : IAuthenticator
    {
        // static properties
        /// <summary>
        /// Gets the name of the mechanism.
        /// </summary>
        /// <value>
        /// The name of the mechanism.
        /// </value>
        public static string MechanismName
        {
            get { return "MONGODB-CR"; }
        }

        // fields
        private readonly UsernamePasswordCredential _credential;

        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="MongoDBCRAuthenticator"/> class.
        /// </summary>
        /// <param name="credential">The credential.</param>
        public MongoDBCRAuthenticator(UsernamePasswordCredential credential)
        {
            _credential = Ensure.IsNotNull(credential, nameof(credential));
        }

        // properties
        /// <inheritdoc/>
        public string Name
        {
            get { return MechanismName; }
        }

        // public methods
        /// <inheritdoc/>
        public void Authenticate(IConnection connection, ConnectionDescription description, CancellationToken cancellationToken)
        {
            Authenticate(connection, description, null, cancellationToken);
        }

        /// <inheritdoc/>
        public void Authenticate(IConnection connection, ConnectionDescription description, ServerApi serverApi, CancellationToken cancellationToken)
        {
            Ensure.IsNotNull(connection, nameof(connection));
            Ensure.IsNotNull(description, nameof(description));

            try
            {
                var getNonceProtocol = CreateGetNonceProtocol(serverApi);
                var getNonceReply = getNonceProtocol.Execute(connection, cancellationToken);
                var authenticateProtocol = CreateAuthenticateProtocol(getNonceReply, serverApi);
                authenticateProtocol.Execute(connection, cancellationToken);
            }
            catch (MongoCommandException ex)
            {
                throw CreateException(connection, ex);
            }
        }

        /// <inheritdoc/>
        public async Task AuthenticateAsync(IConnection connection, ConnectionDescription description, CancellationToken cancellationToken)
        {
            await AuthenticateAsync(connection, description, null, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task AuthenticateAsync(IConnection connection, ConnectionDescription description, ServerApi serverApi, CancellationToken cancellationToken)
        {
            Ensure.IsNotNull(connection, nameof(connection));
            Ensure.IsNotNull(description, nameof(description));

            try
            {
                var getNonceProtocol = CreateGetNonceProtocol(serverApi);
                var getNonceReply = await getNonceProtocol.ExecuteAsync(connection, cancellationToken).ConfigureAwait(false);
                var authenticateProtocol = CreateAuthenticateProtocol(getNonceReply, serverApi);
                await authenticateProtocol.ExecuteAsync(connection, cancellationToken).ConfigureAwait(false);
            }
            catch (MongoCommandException ex)
            {
                throw CreateException(connection, ex);
            }
        }

        /// <inheritdoc/>
        public BsonDocument CustomizeInitialIsMasterCommand(BsonDocument isMasterCommand)
        {
            return isMasterCommand;
        }

        // private methods
        private CommandWireProtocol<BsonDocument> CreateAuthenticateProtocol(BsonDocument getNonceReply, ServerApi serverApi)
        {
            var nonce = getNonceReply["nonce"].AsString;
            var command = new BsonDocument
            {
                { "authenticate", 1 },
                { "user", _credential.Username },
                { "nonce", nonce },
                { "key", CreateKey(_credential.Username, _credential.Password, nonce) }
            };
            var protocol = new CommandWireProtocol<BsonDocument>(
                databaseNamespace: new DatabaseNamespace(_credential.Source),
                command: command,
                slaveOk: true,
                resultSerializer: BsonDocumentSerializer.Instance,
                messageEncoderSettings: null,
                serverApi: serverApi);
            return protocol;
        }

        private MongoAuthenticationException CreateException(IConnection connection, Exception ex)
        {
            var message = string.Format("Unable to authenticate username '{0}' on database '{1}'.", _credential.Username, _credential.Source);
            return new MongoAuthenticationException(connection.ConnectionId, message, ex);
        }

        private CommandWireProtocol<BsonDocument> CreateGetNonceProtocol(ServerApi serverApi)
        {
            var command = new BsonDocument("getnonce", 1);
            var protocol = new CommandWireProtocol<BsonDocument>(
                databaseNamespace: new DatabaseNamespace(_credential.Source),
                command: command,
                slaveOk: true,
                resultSerializer: BsonDocumentSerializer.Instance,
                messageEncoderSettings: null,
                serverApi: serverApi);
            return protocol;
        }

        private string CreateKey(string username, SecureString password, string nonce)
        {
            var passwordDigest = AuthenticationHelper.MongoPasswordDigest(username, password);
            using (var md5 = MD5.Create())
            {
                var bytes = Utf8Encodings.Strict.GetBytes(nonce + username + passwordDigest);
                var hash = md5.ComputeHash(bytes);
                return BsonUtils.ToHexString(hash);
            }
        }
    }
}
