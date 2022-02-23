using System;
using FluentAssertions;
using Xunit;
using ZBRA.Mongo.Repository.Impl;

namespace ZBRA.Mongo.Repository.Tests
{
    [Collection("MongoCollection")]
    public class MigrationTests
    {
        private readonly MongoFixture fixture;

        public MigrationTests(MongoFixture fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public async void VerifyMigrationIsSuccessful()
        {
            var mappings = new Mappings();
            mappings.Entity<IntObj>("MyObj11WillBeUnique")
                .Infer(true)
                .Build();
            var repository = new Repository<IntObj>(fixture.Client, fixture.GetDb(), mappings);

            // create legacy objects that will be migrated later
            var objs = new IntObj[]
            {
                new IntObj { Name = "myobj1", Unique = "a" },
                new IntObj { Name = "myobj2", Unique = "b" }
            };
            var ids = await repository.InsertAsync(objs);
            objs[0].Id = ids[0];
            objs[1].Id = ids[1];

            // change mapping to have a unique constraint
            mappings = new Mappings();
            mappings.Entity<IntObj>("MyObj11WillBeUnique")
                .Unique(o => o.Unique)
                .Infer(true)
                .Build();
            repository = new Repository<IntObj>(fixture.Client, fixture.GetDb(), mappings);

            await repository.InsertAsync(new IntObj { Name = "Unique1", Unique = "c" });

            repository
                .Awaiting(r => r.InsertAsync(new IntObj { Name = "Unique2", Unique = "c" }))
                .Should()
                .ThrowExactly<UniqueConstraintException>();

            objs[0].Name = "updated1";
            await repository.UpdateAsync(objs[0]);

            repository
                .Awaiting(r => r.InsertAsync(new IntObj { Name = "Unique3", Unique = "a" }))
                .Should()
                .ThrowExactly<UniqueConstraintException>();

            // deleting a legacy obj should not cause any issues
            await repository.DeleteAsync(objs[1]);
        }

        private class IntObj
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public int Value { get; set; }
            public string Unique { get; set; } = Guid.NewGuid().ToString();
        }
    }
}