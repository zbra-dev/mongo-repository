using FluentAssertions;
using System;
using Xunit;
using ZBRA.Mongo.Repository.Impl;

namespace ZBRA.Mongo.Repository.Tests
{
    [Collection("MongoCollection")]
    public class ConverterTests
    {
        private readonly Repository<PrimitiveObj> repository;

        public ConverterTests(MongoFixture fixture)
        {
            var mappings = new Mappings();
            mappings.Entity<PrimitiveObj>()
                .Infer(true)
                .Build();
            repository = new Repository<PrimitiveObj>(fixture.Client, fixture.GetDb(), mappings);
            repository.Delete(repository.QueryAllAsync().Result.Entities);
        }

        [Fact]
        public void TestPrimitiveTypes_RegularValues()
        {
            var myObj = new PrimitiveObj
            {
                Int = 10,
                IntNull = 10,
                Long = 30,
                LongNull = 30,
                Double = 10.10,
                DoubleNull = 10.10,
                Decimal = 20,
                DecimalNull = 20,
                Bool = true,
                BoolNull = true,
                Byte = 1,
                ByteNull = 1,
                DateTime = DateTime.UtcNow,
                DateTimeNull = DateTime.UtcNow,
                String = "1234",
            };
            myObj.Id = repository.Insert(myObj);
            myObj.Should().BeEquivalentTo(
                repository.FindById(myObj.Id).Value,
                o => o.Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, 1)).WhenTypeIs<DateTime>()
            );
        }

        [Fact]
        public void TestPrimitiveTypes_EmptyValues()
        {
            var myEmpty = new PrimitiveObj();
            myEmpty.Id = repository.Insert(myEmpty);
            myEmpty.Should().BeEquivalentTo(repository.FindById(myEmpty.Id).Value);
        }

        [Fact]
        public void TestPrimitiveTypes_Limits()
        {
            var minObj = new PrimitiveObj()
            {
                Int = int.MinValue,
                IntNull = int.MinValue,
                Long = long.MinValue,
                LongNull = long.MinValue,
                Double = double.MinValue,
                DoubleNull = double.MinValue,
                Decimal = decimal.MinValue,
                DecimalNull = decimal.MinValue,
                DateTime = DateTime.MinValue.ToUniversalTime(),
                DateTimeNull = DateTime.MinValue.ToUniversalTime(),
            };
            minObj.Id = repository.Insert(minObj);
            minObj.Should().BeEquivalentTo(
                repository.FindById(minObj.Id).Value,
                o => o.Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, 1)).WhenTypeIs<DateTime>()
            );

            var maxObj = new PrimitiveObj()
            {
                Int = int.MaxValue,
                IntNull = int.MaxValue,
                Long = long.MaxValue,
                LongNull = long.MaxValue,
                Double = double.MaxValue,
                DoubleNull = double.MaxValue,
                Decimal = decimal.MaxValue,
                DecimalNull = decimal.MaxValue,
                DateTime = DateTime.MaxValue.ToUniversalTime(),
                DateTimeNull = DateTime.MaxValue.ToUniversalTime(),
            };
            maxObj.Id = repository.Insert(maxObj);
            maxObj.Should().BeEquivalentTo(
                repository.FindById(maxObj.Id).Value,
                o => o.Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, 1)).WhenTypeIs<DateTime>()
            );
        }

        [Theory]
        [InlineData(0.9999999999987423)]
        [InlineData(0.49999999999999994)]
        [InlineData(0.41111111111111114)]
        [InlineData(0.11111111111111114)]
        [InlineData(0.11111111111111119)]
        [InlineData(0.11111111111111111)]
        [InlineData(0.89193459872645983)]
        public void TestPrimitiveTypes_DoublePrecision(double value)
        {
            var myObj = new PrimitiveObj()
            {
                Double = value,
                DoubleNull = value,
            };
            myObj.Id = repository.Insert(myObj);
            myObj.Should().BeEquivalentTo(repository.FindById(myObj.Id).Value);
        }

        [Theory]
        [InlineData(1.123456789012345678901234567890)]
        [InlineData(1234567890.123456789012345678901234567890)]
        public void TestPrimitiveTypes_DecimalPrecision(decimal value)
        {
            var myObj = new PrimitiveObj()
            {
                Decimal = value,
                DecimalNull = value,
            };
            myObj.Id = repository.Insert(myObj);
            myObj.Should().BeEquivalentTo(repository.FindById(myObj.Id).Value);
        }

        private class PrimitiveObj
        {
            public string Id { get; set; }
            public int Int { get; set; }
            public int? IntNull { get; set; }
            public long Long { get; set; }
            public long? LongNull { get; set; }
            public double Double { get; set; }
            public double? DoubleNull { get; set; }
            public bool Bool { get; set; }
            public bool? BoolNull { get; set; }
            public byte Byte { get; set; }
            public byte? ByteNull { get; set; }
            public decimal Decimal { get; set; }
            public decimal? DecimalNull { get; set; }
            public string String { get; set; }
            public DateTime DateTime { get; set; } = DateTime.MinValue.ToUniversalTime();
            public DateTime? DateTimeNull { get; set; }
        }
    }
}
