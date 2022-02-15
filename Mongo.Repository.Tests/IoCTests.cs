using FluentAssertions;
using Google.Cloud.Mongo.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Mongo.Repository.Tests
{
    [Collection("DatastoreCollection")]
    public class IoCTests
    {
        public IoCTests(DatastoreFixture fixture)
        {
        }

        [Fact]
        public async void GetDatastoreDb_Success()
        {
            Environment.SetEnvironmentVariable("DATASTORE_EMULATOR_HOST", "127.0.0.1:8081");
            var inMemory = new Dictionary<string, string>()
            {
                { "Datastore:NamespaceId", "test" },
                { "Datastore:ProjectId", "test-project" },
            };

            var mappings = new Mappings();
            mappings.Entity<MyObjY>().Infer(true).Build();
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddInMemoryCollection(inMemory)
                .Build();
            var provider = new ServiceCollection()
                .AddSingleton(mappings)
                .AddMongoRepository(configuration)
                .BuildServiceProvider();

            var config = provider.GetService<MongoConfig>();
            config.NamespaceId.Should().Be("test");
            config.ProjectId.Should().Be("test-project");

            var db = provider.GetService<DatastoreDb>();
            var entities = await db.QueryAllAsync();
            entities.Should().BeEmpty();

            var repository = provider.GetService<IRepository<MyObjY>>();
            await repository.InsertAsync(new MyObjY { Value = "1" });
            var records = await repository.QueryAllAsync();
            records.Entities.Should().HaveCount(1);
            records.Entities.First().Value.Should().Be("1");
        }

        [Fact]
        public void DatastoreNamespace_Override_Success()
        {
            Environment.SetEnvironmentVariable("DATASTORE_EMULATOR_HOST", "127.0.0.1:8081");
            var inMemory = new Dictionary<string, string>()
            {
                { "Datastore:NamespaceId", "test" },
                { "Datastore:ProjectId", "test-project" },
                { "DatastoreNamespace", "test2" },
            };

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddInMemoryCollection(inMemory)
                .Build();
            var provider = new ServiceCollection()
                .AddMongoRepository(configuration)
                .BuildServiceProvider();
            var config = provider.GetService<MongoConfig>();
            config.NamespaceId.Should().Be("test2");
        }

        public class MyObjY
        {
            public string Id { get; set; }
            public string Value { get; set; }
        }
    }
}