using FluentAssertions;
using System;
using System.Collections.Generic;
using Xunit;

namespace Mongo.Repository.Tests
{
    public class MongoConfigTests
    {

        [Theory]
        [MemberData(nameof(GetInvalidDataConfiguration))]
        public void Validate_DatastoreConfiguration_ShouldFail(MongoConfig datastoreConfig)
        {
            datastoreConfig
                .Invoking(c => c.Validate())
                .Should()
                .Throw<Exception>()
                .Which.Message
                .Should()
                .Contain("Invalid Mongo configuration");
        }

        [Theory]
        [MemberData(nameof(GetValidDataConfiguration))]
        public void Validate_DataStoreConfiguration_Success(MongoConfig datastoreConfig)
        {
            datastoreConfig.Validate();
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