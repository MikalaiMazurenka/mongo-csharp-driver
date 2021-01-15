/* Copyright 2021-present MongoDB Inc.
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
using System.Text;
using MongoDB.Driver.Core.Misc;
using MongoDB.Shared;

namespace MongoDB.Driver
{
    /// <summary>
    /// Represents a server api.
    /// </summary>
    public class ServerApi : IEquatable<ServerApi>
    {
        // fields
        private readonly bool? _deprecatationErrors;
        private readonly bool? _strict;
        private readonly ServerApiVersion _version;

        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerApi" /> class.
        /// </summary>
        /// <param name="version">The server API version.</param>
        public ServerApi(ServerApiVersion version)
        {
            _version = Ensure.IsNotNull(version, nameof(version));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerApi" /> class.
        /// </summary>
        /// <param name="version">The server API version.</param>
        /// <param name="strict">The flag for strict server API version enforcement.</param>
        /// <param name="deprecatationErrors">The flag for treating deprecated server APIs as errors.</param>
        public ServerApi(
            ServerApiVersion version,
            bool? strict,
            bool? deprecatationErrors)
            : this(version)
        {
            _strict = strict;
            _deprecatationErrors = deprecatationErrors;
        }

        // operators
        /// <inheritdoc/>
        public static bool operator !=(ServerApi lhs, ServerApi rhs)
        {
            return !(lhs == rhs);
        }

        /// <inheritdoc/>
        public static bool operator ==(ServerApi lhs, ServerApi rhs)
        {
            return object.Equals(lhs, rhs);
        }

        // properties
        /// <summary>
        /// Gets the deprecation errors flag.
        /// </summary>
        public bool? DeprecationErrors => _deprecatationErrors;

        /// <summary>
        /// Gets the strict flag.
        /// </summary>
        public bool? Strict => _strict;

        /// <summary>
        /// Gets the server API version.
        /// </summary>
        public ServerApiVersion Version => _version;

        // methods
        /// <inheritdoc/>
        public bool Equals(ServerApi other)
        {
            if (other == null)
            {
                return false;
            }

            return
                _deprecatationErrors == other._deprecatationErrors &&
                _strict == other._strict &&
                _version == other._version;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return Equals(obj as ServerApi);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return new Hasher()
                .Hash(_deprecatationErrors)
                .Hash(_strict)
                .Hash(_version)
                .GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var stringBuilder = new StringBuilder($"{{ Version : {_version}");
            if (_strict.HasValue)
            {
                stringBuilder.Append($", Strict : {_strict}");
            }
            if (_deprecatationErrors.HasValue)
            {
                stringBuilder.Append($", DeprecationErrors : {_deprecatationErrors}");
            }
            stringBuilder.Append(" }");

            return stringBuilder.ToString();
        }
    }
}
