using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using System.Linq;
using Xunit;

namespace ZBRA.Mongo.Repository.Tests
{
    [Collection("MongoCollection")]
    public class IoCTests
    {
        private readonly MongoFixture fixture;

        public IoCTests(MongoFixture fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public async void GetMongoDb_Success()
        {
            var config = new MongoConfig
            {
                ConnString = fixture.ConnectionString,
                DatabaseName = "foo"
            };
            var mappings = new Mappings();
            mappings.Entity<MyObjY>().Infer(true).Build();
            var provider = new ServiceCollection()
                .AddSingleton(mappings)
                .AddMongoRepository(config)
                .BuildServiceProvider();

            var injectedConfig = provider.GetService<MongoConfig>();
            injectedConfig.Should().BeEquivalentTo(config);

            var db = provider.GetService<IMongoDatabase>();
            var entities = await db.QueryAllAsync();
            entities.Should().BeEmpty();

            var repository = provider.GetService<IRepository<MyObjY>>();
            await repository.InsertAsync(new MyObjY { Value = "1" });
            var records = await repository.QueryAllAsync();
            records.Entities.Should().HaveCount(1);
            records.Entities.First().Value.Should().Be("1");
        }

        public class MyObjY
        {
            public string Id { get; set; }
            public string Value { get; set; }
        }
    }
}