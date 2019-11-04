﻿/* Copyright 2017-present MongoDB Inc.
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
using MongoDB.Bson;

namespace MongoDB.Driver
{
    /// <summary>
    /// The interface for a core server session.
    /// </summary>
    public interface ICoreServerSession : IDisposable
    {
        /// <summary>
        /// Gets the session Id.
        /// </summary>
        /// <value>
        /// The session Id.
        /// </value>
        BsonDocument Id { get; }

        // properties
        /// <summary>
        /// Gets a value indicate whether this session is dirty.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the session is dirty.
        /// </value>
        bool IsDirty { get; }

        /// <summary>
        /// Gets the time this server session was last used (in UTC).
        /// </summary>
        /// <value>
        /// The time this server session was last used (in UTC).
        /// </value>
        DateTime? LastUsedAt { get; }

        // methods
        /// <summary>
        /// Gets the next transaction number.
        /// </summary>
        /// <returns>The transaction number.</returns>
        long AdvanceTransactionNumber();

        /// <summary>
        /// Marks the session as dirty.
        /// </summary>
        void MarkDirty();

        /// <summary>
        /// Called by the driver when the session is used (i.e. sent to the server).
        /// </summary>
        void WasUsed();
    }
}
