using Mongo.Repository.Impl;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Mongo.Repository.Tests
{
    [Collection("MongoCollection")]
    public class MapTests
    {
        private readonly Repository<MapObj> repository;

        public MapTests(MongoFixture fixture)
        {
            var mappings = new Mappings();
            mappings.Entity<MapObj>()
                .Infer(true)
                .Build();
            repository = new Repository<MapObj>(fixture.Client, fixture.GetDb(), mappings);
            repository.Delete(repository.QueryAllAsync().Result.Entities);
        }

        [Fact]
        public void TestMap()
        {
            var myObj = new MapObj()
            {
                Data = new Dictionary<string, object>
                {
                    { "bool", true },
                    { "string", "a" },
                    { "int", 10 },
                    { "null", null },
                }
            };
            myObj.Id = repository.Insert(myObj);
            myObj.Should().BeEquivalentTo(repository.FindById(myObj.Id).Value);
        }

        [Fact]
        public void TestMapWithJson()
        {
            var data = new Dictionary<string, object>
            {
                { "bool", true },
                { "string", "a" },
                { "int", 10 },
                { "null", null },
            };
            var jsonData = JsonSerializer.Serialize(data);
            var myObj = new MapObj()
            {
                Data = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonData)
            };
            myObj.Id = repository.Insert(myObj);
            var found = repository.FindById(myObj.Id).Value;
            data.Should().BeEquivalentTo(found.Data);
        }

        [Theory]
        [MemberData(nameof(InvalidTypeValues))]
        public void TestMapInvalidTypes(object value)
        {
            var myObj = new MapObj()
            {
                Data = new Dictionary<string, object>
                {
                    { "val", value },
                }
            };
            repository
                .Invoking(repo => repo.Insert(myObj))
                .Should()
                .Throw<ArgumentException>();
        }

        public static IEnumerable<object[]> InvalidTypeValues()
        {
            yield return new object[] { 10.10f };
            yield return new object[] { 10.10d };
            yield return new object[] { 10.10m };
            yield return new object[] { DateTime.UtcNow };
            yield return new object[] { new object() };
            yield return new object[] { new Whatever() };
        }

        public class Whatever
        {

        }

        public class MapObj
        {
            public string Id { get; set; }
            public Dictionary<string, object> Data { get; set; }
        }
    }
}
