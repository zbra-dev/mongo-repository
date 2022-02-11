using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using Xunit;

namespace Mongo.Repository.Tests
{
    public class DatastoreConfigTests
    {

        [Theory]
        [MemberData(nameof(GetInvalidDataConfiguration))]
        public void Validate_DatastoreConfiguration_ShouldFail(DatastoreConfig datastoreConfig, bool emulatorEnabled)
        {
            datastoreConfig
                .Invoking(c => c.Validate(emulatorEnabled))
                .Should()
                .Throw<Exception>()
                .Which.Message
                .Should()
                .Contain("Invalid Datastore configuration");
        }

        [Theory]
        [MemberData(nameof(GetValidDataConfiguration))]
        public void Validate_DataStoreConfiguration_Success(DatastoreConfig datastoreConfig, bool emulatorEnabled)
        {
            datastoreConfig.Validate(emulatorEnabled);
        }

        [Fact]
        public void Read_DatastoreJsonCredentials_Success()
        {
            var inMemory = new Dictionary<string, string>()
            {
                { "Datastore:NamespaceId", "test" },
                { "Datastore:ProjectId", "test-project" },
                { "Datastore:JsonCredentials:Key", "k" },
                { "Datastore:JsonCredentials:Value", "10" },
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemory)
                .Build();

            var datastoreConfig = configuration.GetSection("Datastore").Get<DatastoreConfig>();
            datastoreConfig.JsonCredentials.Should().NotBeEmpty();
            datastoreConfig.JsonCredentials["Key"].Should().Be("k");
            datastoreConfig.JsonCredentials["Value"].Should().Be("10");
        }

        public static IEnumerable<object[]> GetInvalidDataConfiguration()
        {
            yield return new object[]
            {
                new DatastoreConfig
                {
                },
                false
            };

            yield return new object[]
            {
                new DatastoreConfig
                {
                    ProjectId = "",
                    NamespaceId = "test",
                    CredentialsPath = "/tmp/cred.json",
                },
                false
            };

            yield return new object[]
            {
                new DatastoreConfig
                {
                    ProjectId = null,
                    NamespaceId = "test",
                    CredentialsPath = "/tmp/cred.json",
                },
                false
            };

            yield return new object[]
            {
                new DatastoreConfig
                {
                    ProjectId = "test",
                    NamespaceId = "",
                    CredentialsPath = "/tmp/cred.json",
                },
                false
            };

            yield return new object[]
            {
                new DatastoreConfig
                {
                    ProjectId = "test",
                    NamespaceId = null,
                    CredentialsPath = "/tmp/cred.json",
                },
                false
            };

            yield return new object[]
            {
                new DatastoreConfig
                {
                    ProjectId = "test",
                    NamespaceId = "test",
                },
                false
            };

            yield return new object[]
            {
                new DatastoreConfig
                {
                    ProjectId = "test",
                    NamespaceId = "test",
                    CredentialsPath = "",
                },
                false
            };

            yield return new object[]
            {
                new DatastoreConfig
                {
                },
                true
            };

            yield return new object[]
            {
                new DatastoreConfig
                {
                    NamespaceId = "test",
                    ProjectId = null,
                },
                true
            };

            yield return new object[]
            {
                new DatastoreConfig
                {
                    NamespaceId = "test",
                    ProjectId = "",
                },
                true
            };


            yield return new object[]
            {
                new DatastoreConfig
                {
                    NamespaceId = "",
                    ProjectId = "test",
                },
                true
            };

            yield return new object[]
            {
                new DatastoreConfig
                {
                    NamespaceId = null,
                    ProjectId = "test",
                },
                true
            };
        }

        public static IEnumerable<object[]> GetValidDataConfiguration()
        {
            yield return new object[]
            {
                new DatastoreConfig
                {
                    ProjectId = "project",
                    NamespaceId = "test",
                    CredentialsPath = "/tmp/cred.json",
                },
                false
            };

            yield return new object[]
            {
                new DatastoreConfig
                {
                    ProjectId = "project",
                    NamespaceId = "test",
                    JsonCredentials = new Dictionary<string, object>(),
                },
                false
            };

            yield return new object[]
            {
                new DatastoreConfig
                {
                    ProjectId = "project",
                    NamespaceId = "test",
                },
                true
            };
        }
    }
}