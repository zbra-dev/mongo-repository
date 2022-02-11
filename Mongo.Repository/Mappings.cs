using Mongo.Repository.Impl;
using System;
using System.Collections.Generic;

namespace Mongo.Repository
{
    public class Mappings
    {
        private readonly Dictionary<Type, IEntityMapping> map = new Dictionary<Type, IEntityMapping>();

        public MappingBuilder<T> Entity<T>(string name = null) => new MappingBuilder<T>(this, name);
        public MappingBuilder<T, U> Entity<T, U>(string name = null) where U : T => new MappingBuilder<T, U>(this, name);

        public IList<IEntityMigration> FindMigrations<T>() => Get<T>().FindMigrations();

        #region internal

        internal IEntityMapping<T> Get<T>() => (IEntityMapping<T>)map[typeof(T)];
        internal IEntityMapping Get(Type type) => map[type];
        internal bool Contains<T>() => map.ContainsKey(typeof(T));
        internal bool Contains(Type type) => map.ContainsKey(type);

        internal void Add<T>(IEntityMapping<T> mapping) => map[typeof(T)] = mapping;

        #endregion
    }
}