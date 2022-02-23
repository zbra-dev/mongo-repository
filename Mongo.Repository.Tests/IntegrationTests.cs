using System;
using System.Linq;
using FluentAssertions;
using Xunit;
using ZBRA.Mongo.Repository.Impl;

namespace ZBRA.Mongo.Repository.Tests
{
    [Collection("MongoCollection")]
    public class IntegrationTests
    {
        private readonly Repository<IntObj> repository;

        public IntegrationTests(MongoFixture fixture)
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
        public async void DoInsertDelete()
        {
            var x = new IntObj { Name = "myobj1", Value = 0 };
            var id = await repository.InsertAsync(x);
            id.Should().NotBeNull();

            var result = await repository.QueryAllAsync();
            result.Entities.Where(e => e.Value == 0).Should().HaveCount(1);

            await repository.DeleteAsync(result.Entities.Where(e => e.Value == 0).ToArray());

            result = await repository.QueryAsync(o => o.Value, 0);
            result.Entities.Should().BeEmpty();
        }

        [Fact]
        public async void DoMultipleInsert()
        {
            var objs = Enumerable.Range(0, 10).Select(i => new IntObj { Name = $"myobj{i}", Value = 100 + (i / 5) }).ToArray();
            var ids = await repository.InsertAsync(objs);
            ids.Should().HaveCount(10);

            var result = await repository.QueryAsync(o => o.Name, "myobj0");
            result.Entities.Should().HaveCount(1);
            result.Entities.First().Name.Should().Be("myobj0");

            result = await repository.QueryAsync(o => o.Value, 100);
            result.Entities.Should().HaveCount(5);
            result.Entities.All(e => e.Value == 100).Should().BeTrue();

            result = await repository.QueryAsync(o => o.Value, 101);
            result.Entities.Should().HaveCount(5);
            result.Entities.All(e => e.Value == 101).Should().BeTrue();
        }

        [Fact]
        public async void CannotInsertDuplicatesInSameTransaction()
        {
            var ids = await repository.InsertAsync(new IntObj { Name = "myobj1" });
            var objs = new[]
            {
                new IntObj { Unique = "a" },
                new IntObj { Unique = "a" },
            };
            var session = await repository.StartSessionAsync();
            session.StartTransaction();
            repository
                .Awaiting(r => r.InsertAsync(objs, session))
                .Should()
                .ThrowExactly<UniqueConstraintException>();
            await session.AbortTransactionAsync();
            var result = await repository.QueryAllAsync();
            result.Entities.Should().HaveCount(1);
        }

        [Fact]
        public async void CannotInsertDuplicatesIfExistsBefore()
        {
            await repository.InsertAsync(new IntObj { Name = "myobj1", Unique = "a" });

            var objs = new[]
            {
                new IntObj { Unique = "a" },
            };
            repository
                .Awaiting(r => r.InsertAsync(objs))
                .Should()
                .ThrowExactly<UniqueConstraintException>();
            var result = await repository.QueryAllAsync();
            result.Entities.Should().HaveCount(1);

            objs = new[]
            {
                new IntObj { Unique = "a" },
                new IntObj { Unique = "b" },
                new IntObj { Unique = "c" },
            };
            repository
                .Awaiting(r => r.InsertAsync(objs))
                .Should()
                .ThrowExactly<UniqueConstraintException>();
            result = await repository.QueryAllAsync();
            result.Entities.Should().HaveCount(1);

            objs = new[]
            {
                new IntObj { Unique = "a" },
                new IntObj { Unique = "a" },
                new IntObj { Unique = "a" },
            };
            repository
                .Awaiting(r => r.InsertAsync(objs))
                .Should()
                .ThrowExactly<UniqueConstraintException>();
            result = await repository.QueryAllAsync();
            result.Entities.Should().HaveCount(1);

            objs = new[]
            {
                new IntObj { Unique = "b" },
                new IntObj { Unique = "c" },
            };
            var ids = await repository.InsertAsync(objs);
            ids.Should().HaveCount(2);
            result = await repository.QueryAllAsync();
            result.Entities.Should().HaveCount(3);

            await repository.DeleteAsync(result.Entities.Where(o => o.Unique == "a").ToArray());
            result = await repository.QueryAllAsync();
            result.Entities.Should().HaveCount(2);
            objs = new[]
            {
                new IntObj { Unique = "a" },
            };
            ids = await repository.InsertAsync(objs);
            ids.Should().HaveCount(1);
            result = await repository.QueryAllAsync();
            result.Entities.Should().HaveCount(3);
        }

        [Fact]
        public async void UpsertIsSupported()
        {
            await repository.InsertAsync(new IntObj { Name = "myobj1", Unique = "a" });
            var result = await repository.QueryAllAsync();
            var entity = result.Entities.First();
            entity.Name = "m";
            entity.Unique = "b";
            var id = await repository.UpsertAsync(entity);
            id.HasValue.Should().BeFalse(); // no records was inserted
            
            (await repository.QueryAllAsync()).Entities.Single().Should().BeEquivalentTo(entity);
        }
        
        [Fact]
        public async void VerifyUniqueWithUpdate()
        {
            var objs = new IntObj[]
            {
                new IntObj { Name = "myobj1", Unique = "a" },
                new IntObj { Name = "myobj2", Unique = "b" }
            };
            await repository.InsertAsync(objs);
            var result = await repository.QueryAllAsync();
            result.Entities.Should().HaveCount(2);

            var a = result.Entities.First(e => e.Unique == "a");
            a.Unique = "c";
            await repository.UpdateAsync(a);
            result = await repository.QueryAllAsync();

            await repository.InsertAsync(new IntObj { Name = "myobj3", Unique = "a" });
            result = await repository.QueryAllAsync();
            result.Entities.Should().HaveCount(3);
            result.Entities.Select(e => e.Unique).Distinct().Should().BeEquivalentTo(new[] { "a", "b", "c" });

            a = result.Entities.First(e => e.Unique == "a");
            a.Unique = "c";
            repository
                .Awaiting(r => r.UpdateAsync(a))
                .Should()
                .ThrowExactly<UniqueConstraintException>();
            result = await repository.QueryAllAsync();
            result.Entities.Should().HaveCount(3);
            result.Entities.Select(e => e.Unique).Distinct().Should().BeEquivalentTo(new[] { "a", "b", "c" });
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