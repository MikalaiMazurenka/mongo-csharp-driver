/* Copyright 2020-present MongoDB Inc.
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

namespace MongoDB.Driver.Core.Misc
{
    /// <summary>
    /// Represents the hint for write operations feature.
    /// </summary>
    /// <seealso cref="MongoDB.Driver.Core.Misc.Feature" />
    public class HintForWriteOperationsFeature : Feature
    {
        private readonly SemanticVersion _shouldThrowExceptionIfServerVersionLessThan;

        /// <summary>
        /// Initializes a new instance of the <see cref="HintForWriteOperationsFeature"/> class.
        /// </summary>
        /// <param name="name">The name of the feature.</param>
        /// <param name="firstSupportedVersion">The first server version that supports the feature.</param>
        /// <param name="shouldThrowExceptionIfServerVersionLessThan">For servers below this version, the driver MUST raise an error if the caller explicitly provides hint value.</param>
        public HintForWriteOperationsFeature(string name, SemanticVersion firstSupportedVersion, SemanticVersion shouldThrowExceptionIfServerVersionLessThan)
            : base(name, firstSupportedVersion)
        {
            _shouldThrowExceptionIfServerVersionLessThan = shouldThrowExceptionIfServerVersionLessThan;
        }

        /// <summary>
        /// Returns true if driver MUST raise an error if the caller explicitly provides hint value.
        /// </summary>
        /// <param name="serverVersion">The server version.</param>
        public bool ShouldThrowIfNeeded(SemanticVersion serverVersion)
        {
            return serverVersion < _shouldThrowExceptionIfServerVersionLessThan;
        }
    }
}
