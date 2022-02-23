using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace ZBRA.Mongo.Repository.Tests
{
    public class MongoConfigTests
    {

        [Theory]
        [MemberData(nameof(GetInvalidDataConfiguration))]
        public void Validate_Configuration_ShouldFail(MongoConfig config)
        {
            config
                .Invoking(c => c.Validate())
                .Should()
                .Throw<Exception>()
                .Which.Message
                .Should()
                .Contain("Invalid Mongo configuration");
        }

        [Theory]
        [MemberData(nameof(GetValidDataConfiguration))]
        public void Validate_Configuration_Success(MongoConfig config)
        {
            config.Validate();
        }

        public static IEnumerable<object[]> GetInvalidDataConfiguration()
        {
            yield return new object[]
            {
                new MongoConfig
                {
                },
            };

            yield return new object[]
            {
                new MongoConfig
                {
                    ConnString = "",
                    DatabaseName = "test",
                },
            };

            yield return new object[]
            {
                new MongoConfig
                {
                    ConnString = null,
                    DatabaseName = "test",
                },
            };

            yield return new object[]
            {
                new MongoConfig
                {
                    ConnString = "test",
                    DatabaseName = "",
                },
            };

            yield return new object[]
            {
                new MongoConfig
                {
                    ConnString = "test",
                    DatabaseName = null,
                },
            };

            yield return new object[]
            {
                new MongoConfig
                {
                },
            };
        }

        public static IEnumerable<object[]> GetValidDataConfiguration()
        {
            yield return new object[]
            {
                new MongoConfig
                {
                    ConnString = "connection",
                    DatabaseName = "test",
                },
            };
        }
    }
}