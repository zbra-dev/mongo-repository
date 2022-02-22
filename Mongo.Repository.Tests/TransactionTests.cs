using FluentAssertions;
using System;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using ZBRA.Mongo.Repository.Impl;

namespace ZBRA.Mongo.Repository.Tests
{
    [Collection("MongoCollection")]
    public class TransactionTests
    {
        private readonly Repository<IntObj> repository;

        public TransactionTests(MongoFixture fixture)
        {
            var mappings = new Mappings();
            mappings.Entity<IntObj>("MyObj11")
                .Unique(o => o.Unique)
                .Infer(true)
                .Build();
            repository = new Repository<IntObj>(fixture.Client, fixture.GetDb(), mappings);
            repository.Delete(repository.QueryAllAsync().Result.Entities);
        }

        [Fact]
        public async void CommitShouldSucceed()
        {
            var session = await repository.StartSessionAsync();
            session.StartTransaction();
            var objs = new[]
            {
                new IntObj { Unique = "a", Value = 1},
                new IntObj { Unique = "B", Value = 2},
            };
            var ids = await repository.InsertAsync(objs, session);
            await session.CommitTransactionAsync();
            ids.Should().HaveCount(2);
            var result = await repository.QueryAllAsync();
            result.Entities.Should().BeEquivalentTo(objs, opt => opt.Excluding(o => o.Id));
        }
        
        [Fact]
        public async void RollbackShouldSucceed()
        {
            var session = await repository.StartSessionAsync();
            session.StartTransaction();
            var objs = new[]
            {
                new IntObj { Unique = "a", Name = "a", Value = 1},
                new IntObj { Unique = "B", Name = "B", Value = 2},
            };
            var ids = await repository.InsertAsync(objs, session);
            ids.Should().HaveCount(2);

            // Querying outside the transaction should return nothing
            var result = await repository.QueryAllAsync();
            result.Entities.Should().BeEmpty();

            // Querying within the transaction should return the inserted objects
            result = await repository.QueryAllAsync(session: session);
            result.Entities.Should().BeEquivalentTo(objs, opt => opt.Excluding(o => o.Id));

            result = await repository.QueryAsync(o => o.Name, "a", session);
            result.Entities.Should().HaveCount(1);
            result.Entities.First().Should().BeEquivalentTo(objs[0], opt => opt.Excluding(o => o.Id));

            result = await repository.QueryAsync(new FilterByName { Name = "B" }, session);
            result.Entities.Should().HaveCount(1);
            result.Entities.First().Should().BeEquivalentTo(objs[1], opt => opt.Excluding(o => o.Id));

            var found = await repository.FindByIdAsync(ids[0], session);
            found.Value.Should().BeEquivalentTo(objs[0], opt => opt.Excluding(o => o.Id));
            
            // Aborting should revert
            await session.AbortTransactionAsync();
            result = await repository.QueryAllAsync();
            result.Entities.Should().BeEmpty();
        }
        
        private class IntObj
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public int Value { get; set; }
            public string Unique { get; set; } = Guid.NewGuid().ToString();
        }

        private class FilterByName : IFilter<IntObj>
        {
            public int? Take { get; }
            public int? Skip { get; }
            public string Name { get; set; }

            public FilterDefinition<BsonDocument> CreateFilter(IFieldResolver<IntObj> resolver)
            {
                return new BsonDocument("name", Name);
            }
        }
    }
}