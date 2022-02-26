using FluentAssertions;
using Xunit;
using ZBRA.Mongo.Repository.Impl;

namespace ZBRA.Mongo.Repository.Tests
{
    [Collection("MongoCollection")]
    public class IgnorePropertyTests
    {
        private readonly MongoFixture fixture;

        public IgnorePropertyTests(MongoFixture fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public void TestIgnoreProperty()
        {
            var mappings = new Mappings();
            mappings.Entity<SubObj>()
                .Ignore(o => o.Ignore)
                .Infer(true)
                .Build();

            mappings.Entity<IgnoreObj>()
                .Ignore(o => o.Ignore)
                .Ignore(o => o.IgnoreSub)
                .Infer(true)
                .Build();

            var myRepo = new Repository<IgnoreObj>(fixture.Client, fixture.GetDb(), mappings);

            var myObj = new IgnoreObj()
            {
                Name = "a",
                Ignore = "a",
                SubObj = new SubObj
                {
                    Name = "sub-a",
                    Ignore = "sub-a",
                },
                IgnoreSub = new SubObj
                {
                    Name = "sub-b",
                    Ignore = "sub-b",
                }
            };

            myObj.Id = myRepo.Insert(myObj);
            var found = myRepo.FindById(myObj.Id).Value;
            found.Ignore.Should().BeNull();
            found.SubObj.Ignore.Should().BeNull();
            found.IgnoreSub.Should().BeNull();

            myObj.Ignore = null;
            myObj.SubObj.Ignore = null;
            myObj.IgnoreSub = null;
            myObj.Should().BeEquivalentTo(found);
        }

        private class IgnoreObj
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Ignore { get; set; }
            public SubObj SubObj { get; set; }
            public SubObj IgnoreSub { get; set; }
        }

        private class SubObj
        {
            public string Name { get; set; }
            public string Ignore { get; set; }
        }
    }
}
