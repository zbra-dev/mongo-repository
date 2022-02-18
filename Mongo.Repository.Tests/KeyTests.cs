using FluentAssertions;
using MongoDB.Bson;
using System.Threading.Tasks;
using Xunit;
using ZBRA.Mongo.Repository.Impl;

namespace ZBRA.Mongo.Repository.Tests
{
    [Collection("MongoCollection")]
    public class KeyTests
    {
        private readonly MongoFixture fixture;

        public KeyTests(MongoFixture fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public void TestCustomKey()
        {
            var mappings = new Mappings();
            mappings.Entity<KeyObj>()
                .Key(o => o.K)
                .Infer(true)
                .Build();

            var myRepo = new Repository<KeyObj>(fixture.Client, fixture.GetDb(), mappings);

            var myObj = new KeyObj
            {
                Name = "A"
            };
            var id = myRepo.Insert(myObj);
            var found = myRepo.FindById(id).Value;
            found.K.Should().Be(id);
        }

        [Fact]
        public void TestInsertWithKey()
        {
            var mappings = new Mappings();
            mappings.Entity<IdObj>()
                .Infer(true)
                .Build();

            var myRepo = new Repository<IdObj>(fixture.Client, fixture.GetDb(), mappings);

            var myObj = new IdObj
            {
                Id = "999",
                Name = "A"
            };
            myRepo
                .Awaiting(r => r.InsertAsync(myObj))
                .Should()
                .Throw<PersistenceException>()
                .Which.Message.Should()
                .Be("Cannot insert instance that already has key set");
        }

        [Fact]
        public void TestInsertWithCustomKey()
        {
            var mappings = new Mappings();
            mappings.Entity<KeyObj>()
                .Key(o => o.K)
                .Infer(true)
                .Build();

            var myRepo = new Repository<KeyObj>(fixture.Client, fixture.GetDb(), mappings);

            var myObj = new KeyObj
            {
                K = "999",
                Name = "A"
            };
            myRepo
                .Awaiting(r => r.InsertAsync(myObj))
                .Should()
                .Throw<PersistenceException>()
                .Which.Message.Should()
                .Be("Cannot insert instance that already has key set");
        }

        [Fact]
        public void TestUpdateWithKeyNotFound()
        {
            var mappings = new Mappings();
            mappings.Entity<IdObj>()
                .Infer(true)
                .Build();

            var myRepo = new Repository<IdObj>(fixture.Client, fixture.GetDb(), mappings);

            var myObj = new IdObj
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Name = "A"
            };
            myRepo.Update(myObj);

            myRepo.QueryAll().Entities.Should().BeEmpty();
        }

        [Fact]
        public async Task TestUpdateWithoutId()
        {
            var mappings = new Mappings();
            mappings.Entity<IdObj>()
                .Infer(true)
                .Build();

            var repo = new Repository<IdObj>(fixture.Client, fixture.GetDb(), mappings);

            var obj = new IdObj { Name = "Bla" };
            await repo.InsertAsync(obj);

            repo
                .Awaiting(r => r.UpdateAsync(obj))
                .Should()
                .Throw<PersistenceException>()
                .Which.Message.Should()
                .Be("Cannot update an entity that has key null");
        }

        [Fact]
        public async Task TestUpdateWithoutKey()
        {
            var mappings = new Mappings();
            mappings.Entity<KeyObj>()
                .Key(o => o.K)
                .Infer(true)
                .Build();

            var repo = new Repository<KeyObj>(fixture.Client, fixture.GetDb(), mappings);

            var obj = new KeyObj { Name = "Bla" };
            await repo.InsertAsync(obj);

            repo
                .Awaiting(r => r.UpdateAsync(obj))
                .Should()
                .Throw<PersistenceException>()
                .Which.Message.Should()
                .Be("Cannot update an entity that has key null");
        }

        public class KeyObj
        {
            public string K { get; set; }
            public string Name { get; set; }
        }

        public class IdObj
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }
    }
}
