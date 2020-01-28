using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver.Core;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Clusters.ServerSelectors;
using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.Servers;
using Xunit;

namespace MongoDB.Driver.Tests
{
    public class CustomServerSelectorTests
    {
        [Fact]
        public void Should_call_custom_server_selector()
        {
            var eventCapturer = new EventCapturer()
                .Capture<ClusterSelectingServerEvent>()
                .Capture<ClusterSelectedServerEvent>();
            var customServerSelector = new CustomServerSelector();
            var client = DriverTestConfiguration.CreateDisposableClient(
                clientSettings =>
                    clientSettings.ClusterConfigurator =
                        c =>
                        {
                            c.ConfigureCluster(
                                s =>
                                    new ClusterSettings(
                                        serverSelectionTimeout: TimeSpan.FromSeconds(2),
                                        postServerSelector: customServerSelector));
                            c.Subscribe(eventCapturer);
                        });

            var collection = client
                .GetDatabase(DriverTestConfiguration.DatabaseNamespace.DatabaseName)
                .GetCollection<BsonDocument>(DriverTestConfiguration.CollectionNamespace.CollectionName)
                .WithReadPreference(ReadPreference.Nearest);

            for (int i = 0; i < 10; i++)
            {
                eventCapturer.Clear();

                collection.CountDocuments(new BsonDocument());

                customServerSelector.NumberOfCustomServerSelectorCalls.Should().Be(i + 1);
                eventCapturer.Next().Should().BeOfType<ClusterSelectingServerEvent>();
                eventCapturer.Next().Should().BeOfType<ClusterSelectedServerEvent>();
                eventCapturer.Any().Should().BeFalse();
            }
        }

        private class CustomServerSelector : IServerSelector
        {
            public int NumberOfCustomServerSelectorCalls { get; set; }

            public IEnumerable<ServerDescription> SelectServers(ClusterDescription cluster, IEnumerable<ServerDescription> servers)
            {
                NumberOfCustomServerSelectorCalls++;
                var server = servers.FirstOrDefault(x => ((DnsEndPoint)x.EndPoint).Port == 27017);

                return server != null ? new[] { server } : Enumerable.Empty<ServerDescription>();
            }
        }
    }
}
