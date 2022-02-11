using Mongo.Repository.Impl;
using FluentAssertions;
using Google.Cloud.Mongo.V1;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Mongo.Repository.Tests
{
    [Collection("DatastoreCollection")]
    public class SetterTests
    {
        private readonly DatastoreFixture fixture;
        private readonly DatastoreDb datastoreDb;
        private readonly Mappings mappings;

        public SetterTests(DatastoreFixture fixture)
        {
            this.fixture = fixture;
            this.mappings = new Mappings();

            datastoreDb = fixture.GetDb();
            fixture.ClearData();
        }

        [Fact]
        public async Task TestGetter_WithNoSetter_ShouldSuccessed()
        {
            mappings.Entity<NoSetter>()
                .Property(e => e.Data, hasPublicSetter: false)
                .Infer(true)
                .Build();
            var repository = new Repository<NoSetter>(fixture.GetDb(), mappings);
            repository.Delete(repository.QueryAll().Entities);

            var noSetter = new NoSetter();
            noSetter.ChangeData();
            repository.Insert(noSetter);
            var record = repository.QueryAll().Entities.First();
            record.Data.Should().Be("a");

            await repository.InsertAsync(new NoSetter());

            var records = await datastoreDb.QueryAllAsync();
            records.First()["val"].IntegerValue.Should().Be(0);
            records.First()["data"].StringValue.Should().Be("b");

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
                .Invoking(e => e.Property(e => e.Data)
                               .Infer(true)
                               .Build())
                .Should().Throw<ArgumentException>()
                .WithMessage("Property must have a public set method");
        }

        [Fact]
        public void TestWrongConfigurations_NoProperty_ShouldThrowArgumentException()
        {
            mappings.Entity<NoProperty>()
                .Invoking(e => e.Property(e => e.data)
                               .Infer(true)
                               .Build())
                .Should().Throw<ArgumentException>()
                .WithMessage("Expression must be for a property");
        }

        [Fact]
        public void TestWrongConfigurations_PrivateSetter_ShouldThrowArgumentException2()
        {
            mappings.Entity<PrivateSetter>()
                .Invoking(e => e.Property(e => e.Data)
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
            var repository = new Repository<PrivateSetterDate>(fixture.GetDb(), mappings);
            repository.Delete(repository.QueryAll().Entities);

            var privateSetterDate = new PrivateSetterDate();
            privateSetterDate.ChangeData();
            repository.Insert(privateSetterDate);
            var record = repository.QueryAll().Entities.First();
            record.DateTime.Should().Be(day17);

            await repository.InsertAsync(new PrivateSetterDate());

            var records = await datastoreDb.QueryAllAsync();
            records.First()["dateTime"].TimestampValue.Should().Be(day17.ToTimestamp());

            var result = await repository.QueryAsync(new PrivateSetterDateTimestampQuery { Value = day16.ToTimestamp() });
            result.Entities.Should().ContainSingle();
            result.Entities.First().DateTime.Should().Be(day16);

            result = await repository.QueryAsync(new PrivateSetterDateTimestampQuery { Value = day17.ToTimestamp() });
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
            var repository = new Repository<PrivateSetterDate>(fixture.GetDb(), mappings);
            repository.Delete(repository.QueryAll().Entities);

            var privateSetterDate = new PrivateSetterDate();
            privateSetterDate.ChangeData();
            repository.Insert(privateSetterDate);
            var record = repository.QueryAll().Entities.First();
            record.DateTime.Should().Be(day17);

            await repository.InsertAsync(new PrivateSetterDate());

            var records = await datastoreDb.QueryAllAsync();
            records.First()["dateTime"].StringValue.Should().Be(day17.ToString());

            var result = await repository.QueryAsync(new PrivateSetterDateStringQuery { Value = day16.ToString() });
            result.Entities.Should().ContainSingle();
            result.Entities.First().DateTime.Should().Be(day16);

            result = await repository.QueryAsync(new PrivateSetterDateStringQuery { Value = day17.ToString() });
            result.Entities.Should().ContainSingle();
            result.Entities.First().DateTime.Should().Be(day17);
        }

        public class PrivateSetter
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

        public class PrivateSetterDate
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

        public class NoSetter
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

        public class NoProperty
        {
            public string data = "a";
        }

        public class NoSetterStringQuery : IFilter<NoSetter>
        {
            public string Value { get; set; }

            public void ApplyTo(Query query, IFieldResolver<NoSetter> resolver)
            {
                query.Filter = Filter.Equal(resolver.FieldName(s => s.Data), Value);
            }
        }

        public class PrivateSetterDateStringQuery : IFilter<PrivateSetterDate>
        {
            public string Value { get; set; }

            public void ApplyTo(Query query, IFieldResolver<PrivateSetterDate> resolver)
            {
                query.Filter = Filter.Equal(resolver.FieldName(s => s.DateTime), Value);
            }
        }

        public class PrivateSetterDateTimestampQuery : IFilter<PrivateSetterDate>
        {
            public Timestamp Value { get; set; }

            public void ApplyTo(Query query, IFieldResolver<PrivateSetterDate> resolver)
            {
                query.Filter = Filter.Equal(resolver.FieldName(s => s.DateTime), Value);
            }
        }
    }
}
