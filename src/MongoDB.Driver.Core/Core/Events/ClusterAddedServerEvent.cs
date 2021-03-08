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
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Servers;

namespace MongoDB.Driver.Core.Events
{
    /// <summary>
    /// Occurs after a server is added to the cluster.
    /// </summary>
    public struct ClusterAddedServerEvent
    {
        private readonly TimeSpan _duration;
        private readonly DateTime _observedAt;
        private readonly ServerId _serverId;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClusterAddedServerEvent" /> struct.
        /// </summary>
        /// <param name="serverId">The server identifier.</param>
        /// <param name="duration">The duration of time it took to add the server.</param>
        public ClusterAddedServerEvent(ServerId serverId, TimeSpan duration)
        {
            _serverId = serverId;
            _duration = duration;
            _observedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the cluster identifier.
        /// </summary>
        public ClusterId ClusterId
        {
            get { return _serverId.ClusterId; }
        }

        /// <summary>
        /// Gets the duration of time it took to add a server,
        /// </summary>
        public TimeSpan Duration
        {
            get { return _duration; }
        }

        /// <summary>
        /// Gets the observed at time.
        /// </summary>
        public DateTime ObservedAt
        {
            get { return _observedAt; }
        }

        /// <summary>
        /// Gets the server identifier.
        /// </summary>
        public ServerId ServerId
        {
            get { return _serverId; }
        }
    }
}
