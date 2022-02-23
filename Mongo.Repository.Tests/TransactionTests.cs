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
        public async void Insert_ShouldSucceed()
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
        public async void InsertRollback_ShouldBeVisible()
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
            result.Entities.Single().Should().BeEquivalentTo(objs[0], opt => opt.Excluding(o => o.Id));

            result = await repository.QueryAsync(new FilterByName { Name = "B" }, session);
            result.Entities.Single().Should().BeEquivalentTo(objs[1], opt => opt.Excluding(o => o.Id));

            var found = await repository.FindByIdAsync(ids[0], session);
            found.Value.Should().BeEquivalentTo(objs[0], opt => opt.Excluding(o => o.Id));
            
            // Aborting should revert
            await session.AbortTransactionAsync();
            result = await repository.QueryAllAsync();
            result.Entities.Should().BeEmpty();
        }

        [Fact]
        public async void Update_ShouldSucceed()
        {
            var obj = new IntObj {Unique = "a", Value = 1};
            obj.Id = await repository.InsertAsync(obj);

            var session = await repository.StartSessionAsync();
            session.StartTransaction();
            obj.Unique = "b";
            obj.Name = "1";
            obj.Value = 10;
            await repository.UpdateAsync(obj, session);
            await session.CommitTransactionAsync();
            var result = await repository.QueryAllAsync();
            result.Entities.Single().Should().BeEquivalentTo(obj);
        }

        [Fact]
        public async void UpdateRollback_ShouldBeVisible()
        {
            var obj = new IntObj { Unique = "a", Value = 1 };
            obj.Id = await repository.InsertAsync(obj);

            var session = await repository.StartSessionAsync();
            session.StartTransaction();

            var updatedObj = obj.Clone();
            updatedObj.Unique = "b";
            updatedObj.Name = "1";
            updatedObj.Value = 10;
            await repository.UpdateAsync(updatedObj, session);
            
            // Querying outside the transaction should not return new values
            var result = await repository.QueryAllAsync();
            result.Entities.Single().Should().BeEquivalentTo(obj);
            
            // Querying within the transaction should return new values
            result = await repository.QueryAllAsync(session: session);
            result.Entities.Single().Should().BeEquivalentTo(updatedObj);

            // After aborting transaction should return old values
            await session.AbortTransactionAsync();
            result = await repository.QueryAllAsync();
            result.Entities.Single().Should().BeEquivalentTo(obj);
        }
        
        [Fact]
        public async void Delete_ShouldSucceed()
        {
            var obj = new IntObj {Unique = "a", Value = 1};
            obj.Id = await repository.InsertAsync(obj);

            var session = await repository.StartSessionAsync();
            session.StartTransaction();
            await repository.DeleteAsync(obj, session);
            await session.CommitTransactionAsync();
            var result = await repository.QueryAllAsync();
            result.Entities.Should().BeEmpty();
        }

        [Fact]
        public async void DeleteRollback_ShouldBeVisible()
        {
            var obj = new IntObj { Unique = "a", Value = 1 };
            obj.Id = await repository.InsertAsync(obj);

            var session = await repository.StartSessionAsync();
            session.StartTransaction();

            await repository.DeleteAsync(obj.Id, session);
            
            // Querying outside the transaction should still return inserted obj
            var result = await repository.QueryAllAsync();
            result.Entities.Single().Should().BeEquivalentTo(obj);
            
            // Querying within the transaction should return empty
            result = await repository.QueryAllAsync(session: session);
            result.Entities.Should().BeEmpty();

            // After aborting transaction should return old values
            await session.AbortTransactionAsync();
            result = await repository.QueryAllAsync();
            result.Entities.Single().Should().BeEquivalentTo(obj);
        }
        
        private class IntObj
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public int Value { get; set; }
            public string Unique { get; set; } = Guid.NewGuid().ToString();

            public IntObj Clone()
            {
                return (IntObj)MemberwiseClone();
            }
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