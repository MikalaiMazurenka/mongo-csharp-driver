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

using System;
using System.Text;
using System.Text.RegularExpressions;

namespace MongoDB.Driver.Core.Misc
{
    internal class ServerVersion : IEquatable<ServerVersion>, IComparable<ServerVersion>
    {
        // fields
        private readonly string _commitHash;
        private readonly int? _internalBuild;
        private readonly int _major;
        private readonly int _minor;
        private readonly int _patch;
        private readonly int? _releaseCandidate;

        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerVersion"/> class.
        /// </summary>
        /// <param name="major">The major version.</param>
        /// <param name="minor">The minor version.</param>
        /// <param name="patch">The patch version.</param>
        public ServerVersion(int major, int minor, int patch)
            : this(major, minor, patch, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerVersion"/> class.
        /// </summary>
        /// <param name="major">The major version.</param>
        /// <param name="minor">The minor version.</param>
        /// <param name="patch">The patch version.</param>
        /// <param name="releaseCandidate">The release candidate version.</param>
        public ServerVersion(int major, int minor, int patch, int? releaseCandidate)
            : this(major, minor, patch, releaseCandidate, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerVersion"/> class.
        /// </summary>
        /// <param name="major">The major version.</param>
        /// <param name="minor">The minor version.</param>
        /// <param name="patch">The patch version.</param>
        /// <param name="internalBuild">The internal build version.</param>
        /// <param name="commitHash">The internal build commit hash.</param>
        public ServerVersion(int major, int minor, int patch, int? internalBuild, string commitHash)
            : this(major, minor, patch, null, internalBuild, commitHash)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerVersion"/> class.
        /// </summary>
        /// <param name="major">The major version.</param>
        /// <param name="minor">The minor version.</param>
        /// <param name="patch">The patch version.</param>
        /// <param name="releaseCandidate">The release candidate version.</param>
        /// <param name="internalBuild">The internal build version.</param>
        /// <param name="commitHash">The internal build commit hash.</param>
        public ServerVersion(int major, int minor, int patch, int? releaseCandidate, int? internalBuild, string commitHash)
        {
            _major = Ensure.IsGreaterThanOrEqualToZero(major, nameof(major));
            _minor = Ensure.IsGreaterThanOrEqualToZero(minor, nameof(minor));
            _patch = Ensure.IsGreaterThanOrEqualToZero(patch, nameof(patch));
            _releaseCandidate = releaseCandidate; // can be null
            _internalBuild = internalBuild; // can be null
            _commitHash = commitHash; // can be null
        }

        // properties
        /// <summary>
        /// Gets the internal build commit hash.
        /// </summary>
        /// <value>
        /// The internal build commit hash.
        /// </value>
        public string CommitHash
        {
            get { return _commitHash; }
        }

        /// <summary>
        /// Gets the internal build version.
        /// </summary>
        /// <value>
        /// The internal build version.
        /// </value>
        public int? InternalBuild
        {
            get { return _internalBuild; }
        }

        /// <summary>
        /// Gets the major version.
        /// </summary>
        /// <value>
        /// The major version.
        /// </value>
        public int Major
        {
            get { return _major; }
        }

        /// <summary>
        /// Gets the minor version.
        /// </summary>
        /// <value>
        /// The minor version.
        /// </value>
        public int Minor
        {
            get { return _minor; }
        }

        /// <summary>
        /// Gets the patch version.
        /// </summary>
        /// <value>
        /// The patch version.
        /// </value>
        public int Patch
        {
            get { return _patch; }
        }

        /// <summary>
        /// Gets the release candidate version.
        /// </summary>
        /// <value>
        /// The release candidate version.
        /// </value>
        public int? ReleaseCandidate
        {
            get { return _releaseCandidate; }
        }

        // public methods
        public int CompareTo(ServerVersion other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            var result = _major.CompareTo(other._major);
            if (result != 0)
            {
                return result;
            }

            result = _minor.CompareTo(other._minor);
            if (result != 0)
            {
                return result;
            }

            result = _patch.CompareTo(other._patch);
            if (result != 0)
            {
                return result;
            }

            if (_releaseCandidate != null || other._releaseCandidate != null)
            {
                if (_releaseCandidate == null)
                {
                    return 1;
                }
                if (other._releaseCandidate == null)
                {
                    return -1;
                }

                result = _releaseCandidate.Value.CompareTo(other._releaseCandidate.Value);
                if (result != 0)
                {
                    return result;
                }
            }

            if (_internalBuild != null || other._internalBuild != null)
            {
                if (_internalBuild == null)
                {
                    return -1;
                }
                if (other._internalBuild == null)
                {
                    return 1;
                }

                result = _internalBuild.Value.CompareTo(other._internalBuild.Value);
                if (result != 0)
                {
                    return result;
                }
            }

            if (_commitHash == null && other._commitHash == null)
            {
                return 0;
            }
            if (_commitHash == null)
            {
                return -1;
            }
            if (other._commitHash == null)
            {
                return 1;
            }

            return 0;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return Equals(obj as ServerVersion);
        }

        /// <inheritdoc/>
        public bool Equals(ServerVersion other)
        {
            return CompareTo(other) == 0;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder($"{_major}.{_minor}.{_patch}");
            if (_releaseCandidate != null)
            {
                sb.Append($"-rc{_releaseCandidate}");
            }
            if (_internalBuild != null)
            {
                sb.Append($"-{_internalBuild}");
            }
            if (_commitHash != null)
            {
                sb.Append($"-g{_commitHash}");
            }

            return sb.ToString();
        }

        // static methods
        /// <summary>
        /// Parses a string representation of a server version.
        /// </summary>
        /// <param name="value">The string value to parse.</param>
        /// <returns>A server version.</returns>
        /// <exception cref="FormatException">value</exception>
        public static ServerVersion Parse(string value)
        {
            ServerVersion result;
            if (TryParse(value, out result))
            {
                return result;
            }

            throw new FormatException($"Invalid ServerVersion: '{value}'.");
        }

        /// <summary>
        /// Tries to parse a string representation of a server version.
        /// </summary>
        /// <param name="value">The string value to parse.</param>
        /// <param name="result">The result.</param>
        /// <returns>True if the string representation was parsed successfully; otherwise false.</returns>
        public static bool TryParse(string value, out ServerVersion result)
        {
            if (string.IsNullOrEmpty(value))
            {
                result = null;
                return false;
            }
            var pattern = @"^(?<major>\d+)\.(?<minor>\d+)(\.(?<patch>\d+)(-rc(?<releaseCandidate>\d+))?(-(?<internalBuild>\d+)-g(?<commitHash>[0-9a-f]{4,40}))?)?$";
            var match = Regex.Match(value, pattern);

            if (!match.Success)
            {
                result = null;
                return false;
            }

            var major = int.Parse(match.Groups["major"].Value);
            var minor = int.Parse(match.Groups["minor"].Value);
            var patch = match.Groups["patch"].Success ? int.Parse(match.Groups["patch"].Value) : 0;
            var releaseCandidateEntry = match.Groups["releaseCandidate"].Success ? match.Groups["releaseCandidate"].Value : null;
            var internalBuildEntry = match.Groups["internalBuild"].Success ? match.Groups["internalBuild"].Value : null;
            var commitHash = match.Groups["commitHash"].Success ? match.Groups["commitHash"].Value : null;

            int? releaseCandidate = null;
            if (releaseCandidateEntry != null && int.TryParse(releaseCandidateEntry, out int releaseCandidateParsed))
            {
                releaseCandidate = releaseCandidateParsed;
            }
            int? internalBuild = null;
            if (internalBuildEntry != null && int.TryParse(internalBuildEntry, out int internalBuildParsed))
            {
                internalBuild = internalBuildParsed;
            }

            result = new ServerVersion(major, minor, patch, releaseCandidate, internalBuild, commitHash);
            return true;
        }

        // public operators
        /// <summary>
        /// Casts a SemanticVersion to a ServerVersion.
        /// </summary>
        /// <param name="semanticVersion">The SemanticVersion.</param>
        /// <returns>A ServerVersion.</returns>
        /// <exception cref="FormatException">semanticVersion</exception>
        public static explicit operator ServerVersion(SemanticVersion semanticVersion)
        {
            return Parse(semanticVersion.ToString());
        }

        /// <summary>
        /// Determines whether two specified server versions have the same value.
        /// </summary>
        /// <param name="a">The first server version to compare, or null.</param>
        /// <param name="b">The second server version to compare, or null.</param>
        /// <returns>
        /// True if the value of a is the same as the value of b; otherwise false.
        /// </returns>
        public static bool operator ==(ServerVersion a, ServerVersion b)
        {
            if (ReferenceEquals(a, null))
            {
                return ReferenceEquals(b, null);
            }

            return a.CompareTo(b) == 0;
        }

        /// <summary>
        /// Determines whether two specified server versions have different values.
        /// </summary>
        /// <param name="a">The first server version to compare, or null.</param>
        /// <param name="b">The second server version to compare, or null.</param>
        /// <returns>
        /// True if the value of a is different from the value of b; otherwise false.
        /// </returns>
        public static bool operator !=(ServerVersion a, ServerVersion b)
        {
            if (ReferenceEquals(a, null))
            {
                return !ReferenceEquals(b, null);
            }

            return a.CompareTo(b) != 0;
        }

        /// <summary>
        /// Determines whether the first specified ServerVersion is greater than the second specified ServerVersion.
        /// </summary>
        /// <param name="a">The first server version to compare, or null.</param>
        /// <param name="b">The second server version to compare, or null.</param>
        /// <returns>
        /// True if the value of a is greater than b; otherwise false.
        /// </returns>
        public static bool operator >(ServerVersion a, ServerVersion b)
        {
            if (ReferenceEquals(a, null))
            {
                return false;
            }

            return a.CompareTo(b) > 0;
        }

        /// <summary>
        /// Determines whether the first specified ServerVersion is greater than or equal to the second specified ServerVersion.
        /// </summary>
        /// <param name="a">The first server version to compare, or null.</param>
        /// <param name="b">The second server version to compare, or null.</param>
        /// <returns>
        /// True if the value of a is greater than or equal to b; otherwise false.
        /// </returns>
        public static bool operator >=(ServerVersion a, ServerVersion b)
        {
            if (ReferenceEquals(a, null))
            {
                if (ReferenceEquals(b, null))
                {
                    return true;
                }

                return false;
            }

            return a.CompareTo(b) >= 0;
        }

        /// <summary>
        /// Determines whether the first specified ServerVersion is less than the second specified ServerVersion.
        /// </summary>
        /// <param name="a">The first server version to compare, or null.</param>
        /// <param name="b">The second server version to compare, or null.</param>
        /// <returns>
        /// True if the value of a is less than b; otherwise false.
        /// </returns>
        public static bool operator <(ServerVersion a, ServerVersion b)
        {
            if (ReferenceEquals(a, null))
            {
                if (ReferenceEquals(b, null))
                {
                    return false;
                }

                return true;
            }

            return a.CompareTo(b) < 0;
        }

        /// <summary>
        /// Determines whether the first specified ServerVersion is less than or equal to the second specified ServerVersion.
        /// </summary>
        /// <param name="a">The first server version to compare, or null.</param>
        /// <param name="b">The second server version to compare, or null.</param>
        /// <returns>
        /// True if the value of a is less than or equal to b; otherwise false.
        /// </returns>
        public static bool operator <=(ServerVersion a, ServerVersion b)
        {
            if (ReferenceEquals(a, null))
            {
                return true;
            }

            return a.CompareTo(b) <= 0;
        }
    }
}
