using Mongo.Repository.Impl;
using FluentAssertions;
using System;
using Xunit;

namespace Mongo.Repository.Tests
{
    [Collection("DatastoreCollection")]
    public class MigrationTests
    {
        private readonly DatastoreFixture fixture;

        public MigrationTests(DatastoreFixture fixture)
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
            var repository = new Repository<IntObj>(fixture.GetDb(), mappings);

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
            repository = new Repository<IntObj>(fixture.GetDb(), mappings);

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

        [Fact]
        public async void VerifyMigrationWithDuplicates()
        {
            var mappings = new Mappings();
            mappings.Entity<IntObj>("MyObj11WillBeUnique1")
                .Infer(true)
                .Build();
            var repository = new Repository<IntObj>(fixture.GetDb(), mappings);

            // create legacy objects that will be migrated later
            var objs = new IntObj[]
            {
                new IntObj { Name = "myobj1", Unique = "a" },
                new IntObj { Name = "myobj2", Unique = "b" },
                new IntObj { Name = "myobj3", Unique = "a" },
            };
            var ids = await repository.InsertAsync(objs);
            for (var i = 0; i < ids.Length; ++i)
                objs[i].Id = ids[i];

            // change mapping to have a unique constraint
            mappings = new Mappings();
            mappings.Entity<IntObj>("MyObj11WillBeUnique1")
                .Unique(o => o.Unique)
                .Infer(true)
                .Build();
            repository = new Repository<IntObj>(fixture.GetDb(), mappings);

            // insert a duplicate
            var obj = new IntObj { Name = "Unique1", Unique = "b" };
            obj.Id = await repository.InsertAsync(obj);
            await repository.UpdateAsync(objs[0]);
            await repository.UpdateAsync(objs[0]);

            repository
                .Awaiting(r => r.UpdateAsync(objs[2]))
                .Should()
                .ThrowExactly<UniqueConstraintException>();

            await repository.UpdateAsync(obj);
            repository
                .Awaiting(r => r.UpdateAsync(objs[1]))
                .Should()
                .ThrowExactly<UniqueConstraintException>();
        }

        public class IntObj
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public int Value { get; set; }
            public string Unique { get; set; } = Guid.NewGuid().ToString();
        }
    }
}