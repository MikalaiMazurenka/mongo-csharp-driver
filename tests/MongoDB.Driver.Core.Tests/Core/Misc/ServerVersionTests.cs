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
using FluentAssertions;
using Xunit;

namespace MongoDB.Driver.Core.Misc
{
    public class ServerVersionTests
    {
        [Theory]
        [InlineData(null, null, 0)]
        [InlineData(null, "1.0.0", -1)]
        [InlineData("1.0.0", null, 1)]
        [InlineData("1.0.0", "1.0.0", 0)]
        [InlineData("1.1.0", "1.1.0", 0)]
        [InlineData("1.1.1", "1.1.1", 0)]
        [InlineData("1.0.0", "2.0.0", -1)]
        [InlineData("2.0.0", "1.0.0", 1)]
        [InlineData("1.0.0", "1.1.0", -1)]
        [InlineData("1.1.0", "1.0.0", 1)]
        [InlineData("1.0.0", "1.0.1", -1)]
        [InlineData("1.0.1", "1.0.0", 1)]
        [InlineData("4.4.0-rc12", "4.4.0-rc12", 0)]
        [InlineData("4.4.0-rc12", "4.4.0", -1)]
        [InlineData("4.4.1-rc12", "4.4.0", 1)]
        [InlineData("4.4.0-rc13", "4.4.0-rc12", 1)]
        [InlineData("4.5.0-489-gb8f58d7", "4.5.0-489-gb8f58d7", 0)]
        [InlineData("4.5.0-489-gb8f58d7", "4.5.1", -1)]
        [InlineData("4.5.0-489-gb8f58d7", "4.5.0", 1)]
        [InlineData("4.5.0-489-gb8f58d7", "4.5.0-5-g5a9a742f6f", 1)]
        [InlineData("4.4.0-rc12-5-g5a9a742f6f", "4.4.0-rc12-5-g5a9a742f6f", 0)]
        [InlineData("4.4.0-rc12-5-g5a9a742f6f", "4.4.0-rc13", -1)]
        [InlineData("4.4.0-rc12-5-g5a9a742f6f", "4.4.0-rc12", 1)]
        [InlineData("4.4.0-rc12-5-g5a9a742f6f", "4.4.0-rc12-4-g5a9a742f6f", 1)]
        [InlineData("4.4.0-rc12-5-g5a9a742f6f", "4.4.0-rc13-5-g5a9a742f6f", -1)]
        public void Comparisons_should_be_correct(string a, string b, int comparison)
        {
            var subject = a == null ? null : ServerVersion.Parse(a);
            var comparand = b == null ? null : ServerVersion.Parse(b);
            subject?.Equals(comparand).Should().Be(comparison == 0);
            subject?.CompareTo(comparand).Should().Be(comparison);
            (subject == comparand).Should().Be(comparison == 0);
            (subject != comparand).Should().Be(comparison != 0);
            (subject > comparand).Should().Be(comparison == 1);
            (subject >= comparand).Should().Be(comparison >= 0);
            (subject < comparand).Should().Be(comparison == -1);
            (subject <= comparand).Should().Be(comparison <= 0);
        }

        [Theory]
        [InlineData("4.2", 4, 2, 0, null, null, null)]
        [InlineData("1.0.0", 1, 0, 0, null, null, null)]
        [InlineData("1.2.0", 1, 2, 0, null, null, null)]
        [InlineData("1.0.3", 1, 0, 3, null, null, null)]
        [InlineData("1.0.3-rc0", 1, 0, 3, 0, null, null)]
        [InlineData("1.0.3-rc23", 1, 0, 3, 23, null, null)]
        [InlineData("4.5.0-489-gb8f58d7", 4, 5, 0, null, 489, "b8f58d7")]
        [InlineData("4.4.0-rc12-5-g5a9a742f6f", 4, 4, 0, 12, 5, "5a9a742f6f")]
        public void Parse_should_handle_valid_server_version_strings(string versionString, int major, int minor, int patch, int? releaseCandidate, int? internalBuild, string commitHash)
        {
            var subject = ServerVersion.Parse(versionString);

            subject.Major.Should().Be(major);
            subject.Minor.Should().Be(minor);
            subject.Patch.Should().Be(patch);
            subject.ReleaseCandidate.Should().Be(releaseCandidate);
            subject.InternalBuild.Should().Be(internalBuild);
            subject.CommitHash.Should().Be(commitHash);
        }

        [Theory]
        [InlineData("1")]
        [InlineData("1-rc2")]
        [InlineData("alpha")]
        [InlineData("4.2-rc2")]
        [InlineData("1.0.3-rc.1.2.")]
        [InlineData("1.0.3-rc-1")]
        [InlineData("4.5.0--489-gb8f58d7")]
        [InlineData("4.5.0-489--gb8f58d7")]
        [InlineData("4.5.0-489-gb8f58x7")]
        [InlineData("1.0.0-alpha")]
        [InlineData("4.5.0-489")]
        [InlineData("4.5.0-489-b8f58x7")]
        [InlineData("4.4.0-rc12-5-g5a9a742f6f-123")]
        public void Parse_should_throw_a_FormatException_when_the_version_string_is_invalid(string versionString)
        {
            Action act = () => ServerVersion.Parse(versionString);

            act.ShouldThrow<FormatException>();
        }

        [Theory]
        [InlineData("1.0.0", 1, 0, 0, null, null, null)]
        [InlineData("1.2.0", 1, 2, 0, null, null, null)]
        [InlineData("1.0.3", 1, 0, 3, null, null, null)]
        [InlineData("1.0.3-rc13", 1, 0, 3, 13, null, null)]
        [InlineData("4.5.0-489-gb8f58d7", 4, 5, 0, null, 489, "b8f58d7")]
        [InlineData("4.4.0-rc12-5-g5a9a742f6f", 4, 4, 0, 12, 5, "5a9a742f6f")]
        public void ToString_should_render_a_correct_server_version_string(string versionString, int major, int minor, int patch, int? releaseCandidate, int? internalBuild, string commitHash)
        {
            var subject = new ServerVersion(major, minor, patch, releaseCandidate, internalBuild, commitHash);

            subject.ToString().Should().Be(versionString);
        }
    }
}
