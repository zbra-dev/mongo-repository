using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ZBRA.Mongo.Repository.Impl;

namespace ZBRA.Mongo.Repository.Tests
{
    [Collection("MongoCollection")]
    public class SetterTests
    {
        private readonly MongoFixture fixture;
        private readonly IMongoDatabase mongoDb;
        private readonly Mappings mappings;

        public SetterTests(MongoFixture fixture)
        {
            this.fixture = fixture;
            mappings = new Mappings();

            mongoDb = fixture.GetDb();
            fixture.ClearData();
        }

        [Fact]
        public async Task TestGetter_WithNoSetter_ShouldSucceed()
        {
            mappings.Entity<NoSetter>()
                .Property(e => e.Data, hasPublicSetter: false)
                .Infer(true)
                .Build();
            var repository = new Repository<NoSetter>(fixture.Client, fixture.GetDb(), mappings);

            var noSetter = new NoSetter();
            noSetter.ChangeData();
            await repository.InsertAsync(noSetter);
            var record = (await repository.QueryAllAsync()).Entities.First();
            record.Data.Should().Be("a");

            await repository.InsertAsync(new NoSetter());

            var records = await mongoDb.QueryAllAsync();
            records.First()["val"].AsInt32.Should().Be(0);
            records.First()["data"].AsString.Should().Be("b");

            var result = await repository.QueryAsync(new NoSetterStringQuery { Value = "a" });
            result.Entities.Should().ContainSingle();
            result.Entities.First().Data.Should().Be("a");

            result = await repository.QueryAsync(new NoSetterStringQuery { Value = "b" });
            result.Entities.Should().ContainSingle();
            result.Entities.First().Data.Should().Be("a");
        }

        [Fact]
        public void TestWrongConfigurations_NoSetter_ShouldThrowArgumentException()
        {
            mappings.Entity<NoSetter>()
                .Invoking(e => e.Property(i => i.Data)
                               .Infer(true)
                               .Build())
                .Should().Throw<ArgumentException>()
                .WithMessage("Property must have a public set method");
        }

        [Fact]
        public void TestWrongConfigurations_NoProperty_ShouldThrowArgumentException()
        {
            mappings.Entity<NoProperty>()
                .Invoking(e => e.Property(i => i.data)
                               .Infer(true)
                               .Build())
                .Should().Throw<ArgumentException>()
                .WithMessage("Expression must be for a property");
        }

        [Fact]
        public void TestWrongConfigurations_PrivateSetter_ShouldThrowArgumentException2()
        {
            mappings.Entity<PrivateSetter>()
                .Invoking(e => e.Property(i => i.Data)
                               .Infer(true)
                               .Build())
                .Should().Throw<ArgumentException>()
                .WithMessage("Property must have a public set method");
        }


        [Fact]
        public async Task TestWrongConfigurations_WithIsGetterTrue_PrivateSetterAsync()
        {
            var day16 = DateTime.ParseExact("2021-12-16 03:00:00Z", "u", CultureInfo.InvariantCulture).ToUniversalTime();
            var day17 = DateTime.ParseExact("2021-12-17 03:00:00Z", "u", CultureInfo.InvariantCulture).ToUniversalTime();

            mappings.Entity<PrivateSetterDate>()
                .Property(e => e.DateTime, hasPublicSetter: false)
                .Infer(true)
                .Build();
            var repository = new Repository<PrivateSetterDate>(fixture.Client, fixture.GetDb(), mappings);
            await repository.DeleteAsync((await repository.QueryAllAsync()).Entities);

            var privateSetterDate = new PrivateSetterDate();
            privateSetterDate.ChangeData();
            await repository.InsertAsync(privateSetterDate);
            var record = (await repository.QueryAllAsync()).Entities.First();
            record.DateTime.Should().Be(day17);

            await repository.InsertAsync(new PrivateSetterDate());

            var records = await mongoDb.QueryAllAsync();
            records.First()["dateTime"].AsBsonDateTime.Should().Be(new BsonDateTime(day17));

            var result = await repository.QueryAsync(new PrivateSetterDateTimeQuery { Value = new BsonDateTime(day16) });
            result.Entities.Should().ContainSingle();
            result.Entities.First().DateTime.Should().Be(day16);

            result = await repository.QueryAsync(new PrivateSetterDateTimeQuery { Value = new BsonDateTime(day17) });
            result.Entities.Should().ContainSingle();
            result.Entities.First().DateTime.Should().Be(day17);
        }

        [Fact]
        public async Task TestWrongConfigurations_WithIsGetterFalse_PrivateSetterAsync()
        {
            var day16 = DateTime.ParseExact("2021-12-16 03:00:00Z", "u", CultureInfo.InvariantCulture).ToUniversalTime();
            var day17 = DateTime.ParseExact("2021-12-17 03:00:00Z", "u", CultureInfo.InvariantCulture).ToUniversalTime();

            mappings.Entity<PrivateSetterDate>()
                .Property(e => e.DateTime, e => DateTime.Parse(e), e => e.ToString())
                .Infer(true)
                .Build();
            var repository = new Repository<PrivateSetterDate>(fixture.Client, fixture.GetDb(), mappings);
            await repository.DeleteAsync((await repository.QueryAllAsync()).Entities);

            var privateSetterDate = new PrivateSetterDate();
            privateSetterDate.ChangeData();
            await repository.InsertAsync(privateSetterDate);
            var record = (await repository.QueryAllAsync()).Entities.First();
            record.DateTime.Should().Be(day17);

            await repository.InsertAsync(new PrivateSetterDate());

            var records = await mongoDb.QueryAllAsync();
            records.First()["dateTime"].AsString.Should().Be(day17.ToString());

            var result = await repository.QueryAsync(new PrivateSetterDateStringQuery { Value = day16.ToString() });
            result.Entities.Should().ContainSingle();
            result.Entities.First().DateTime.Should().Be(day16);

            result = await repository.QueryAsync(new PrivateSetterDateStringQuery { Value = day17.ToString() });
            result.Entities.Should().ContainSingle();
            result.Entities.First().DateTime.Should().Be(day17);
        }

        private class PrivateSetter
        {
            public string Id { get; set; }
            public string Data { get; private set; }

            public PrivateSetter()
            {
                Data = "a";
            }

            public void ChangeData()
            {
                Data = "b";
            }
        }

        private class PrivateSetterDate
        {
            public string Id { get; set; }
            public DateTime? DateTime { get; private set; }

            public PrivateSetterDate()
            {
                DateTime = System.DateTime.ParseExact("2021-12-16 03:00:00Z", "u", CultureInfo.InvariantCulture).ToUniversalTime();
            }

            public void ChangeData()
            {
                DateTime = System.DateTime.ParseExact("2021-12-17 03:00:00Z", "u", CultureInfo.InvariantCulture).ToUniversalTime();
            }
        }

        private class NoSetter
        {
            private string data = "a";

            public string Id { get; set; }
            public string Data { get => data; }
            public int Val { get; set; }

            public void ChangeData()
            {
                data = "b";
            }
        }

        private class NoProperty
        {
            public string data = "a";
        }

        private class NoSetterStringQuery : IFilter<NoSetter>
        {
            public string Value { get; set; }

            public int? Take => null;
            public int? Skip => null;

            public FilterDefinition<BsonDocument> CreateFilter(IFieldResolver<NoSetter> resolver)
            {
                var name = resolver.FieldName(s => s.Data);
                return Builders<BsonDocument>.Filter.Eq(name, Value);
            }
        }

        private class PrivateSetterDateStringQuery : IFilter<PrivateSetterDate>
        {
            public string Value { get; set; }

            public int? Take => null;

            public int? Skip => null;

            public FilterDefinition<BsonDocument> CreateFilter(IFieldResolver<PrivateSetterDate> resolver)
            {
                var name = resolver.FieldName(s => s.DateTime);
                return Builders<BsonDocument>.Filter.Eq(name, Value);
            }
        }

        private class PrivateSetterDateTimeQuery : IFilter<PrivateSetterDate>
        {
            public BsonDateTime Value { get; set; }

            public int? Take => null;

            public int? Skip => null;

            public FilterDefinition<BsonDocument> CreateFilter(IFieldResolver<PrivateSetterDate> resolver)
            {
                var name = resolver.FieldName(s => s.DateTime);
                return Builders<BsonDocument>.Filter.Eq(name, Value);
                throw new NotImplementedException();
            }
        }
    }
}
