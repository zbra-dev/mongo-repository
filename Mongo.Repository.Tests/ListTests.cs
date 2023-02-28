using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using ZBRA.Mongo.Repository.Impl;

namespace ZBRA.Mongo.Repository.Tests
{
    [Collection("MongoCollection")]
    public class ListTests
    {
        private readonly Repository<ListObj> repository;

        public ListTests(MongoFixture fixture)
        {
            var mappings = new Mappings();
            mappings.Entity<ListObj>()
                .Infer(true)
                .Build();
            repository = new Repository<ListObj>(fixture.Client, fixture.GetDb(), mappings);
            repository.Delete(repository.QueryAllAsync().Result.Entities);
        }

        [Fact]
        public void TestList()
        {
            var listObj = new ListObj
            {
                List = new List<string> { "1" },
                ReadOnlyList = new List<string> { "2" },
            };
            listObj.Id = repository.Insert(listObj);
            listObj.Should().BeEquivalentTo(repository.FindById(listObj.Id).Value);
        }

        private class ListObj
        {
            public string Id { get; set; }
            public IList<string> List { get; set; }
            public IReadOnlyList<string> ReadOnlyList { get; set; }
        }
    }
}
