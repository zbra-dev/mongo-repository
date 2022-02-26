using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using ZBRA.Mongo.Repository.Impl;

namespace ZBRA.Mongo.Repository.Tests
{
    public class UnsupportedTypeTests
    {
        [Fact]
        public void TestObject()
        {
            var mappings = new Mappings();

            mappings.Entity<UnsupportedTypes>()
                .Invoking(b => b.Property(o => o.Obj).Build())
                .Should().Throw<ArgumentException>();

            mappings.Entity<UnsupportedTypes>()
              .Invoking(b => b.Property(o => o.Map).Build())
              .Should().Throw<ArgumentException>();

            mappings.Entity<UnsupportedTypes>()
              .Invoking(b => b.Property(o => o.Float).Build())
              .Should().Throw<ArgumentException>();

            mappings.Entity<UnsupportedTypes>()
              .Invoking(b => b.Property(o => o.UnmappedClass).Build())
              .Should().Throw<EntityMappingNotFoundException>();
        }

        public class UnsupportedTypes
        {
            public object Obj { get; set; }
            public Dictionary<object, object> Map { get; set; }
            public float Float { get; set; }
            public UnmappedClass UnmappedClass { get; set; }
        }

        public class UnmappedClass
        {
            public string Name { get; set; }
        }
    }
}
