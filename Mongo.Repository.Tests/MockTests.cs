using FluentAssertions;
using System;
using System.Linq;
using Xunit;
using ZBRA.Mongo.Repository.Mock;

namespace ZBRA.Mongo.Repository.Tests
{
    public class MockTests
    {
        [Fact]
        public void CheckUniqueConstraintSupport()
        {
            var mappings = new Mappings();
            mappings.Entity<MockObj>()
                .Unique(o => o.Name)
                .Infer(true)
                .Build();
            mappings.Invoking(m => new RepositoryMock<MockObj>(m)).Should().Throw<ArgumentException>();
        }

        [Fact]
        public async void BasicOperations()
        {
            var mappings = new Mappings();
            mappings.Entity<MockObj>()
                .Infer(true)
                .Build();
            var repository = new RepositoryMock<MockObj>(mappings);

            var objs = Enumerable.Range(0, 10).Select(i =>
            {
                return new MockObj
                {
                    Name = "obj1",
                    Value = i + 100,
                };
            }).ToArray();

            await repository.InsertAsync(objs);
            var result = await repository.QueryAllAsync();
            result.Entities.Should().HaveCount(objs.Length);

            await repository.DeleteAsync(result.Entities.First(e => e.Value == 100));
            result = await repository.QueryAllAsync();
            result.Entities.Should().HaveCount(objs.Length - 1);

            var first = result.Entities.First(e => e.Value == 101);
            first.Name = "a";
            await repository.UpdateAsync(first);

            result = await repository.QueryAsync(e => e.Value, 101);
            result.Entities.First().Name.Should().Be("a");
            result = await repository.QueryAsync(e => e.Name, "a");
            result.Entities.First().Value.Should().Be(101);

            // there's no point in testing this class further since RepositoryMock 
            // should be deprecated and its use is discouraged
        }

        public class MockObj
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public int Value { get; set; }
        }
    }
}
