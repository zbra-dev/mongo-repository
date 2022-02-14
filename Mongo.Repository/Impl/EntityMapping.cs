using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ZBRA.Commons;

namespace Mongo.Repository.Impl
{
    internal interface IEntityMapping
    {
        object FromEntity(BsonDocument entity);
        BsonDocument ToEntity(object instance, Func<BsonDocument, ObjectId> keyFactory = null);
        IList<IEntityMigration> FindMigrations();
    }

    internal interface IEntityMapping<T> : IEntityMapping
    {
        string Name { get; }
        Maybe<Func<T, string>> UniqueProperty { get; }

        new T FromEntity(BsonDocument entity);
        BsonDocument ToEntity(T instance, Func<BsonDocument, ObjectId> keyFactory = null);
        Maybe<string> GetFieldName(PropertyInfo propertyInfo);
        Maybe<string> GetFieldName(string propertyName);
        BsonValue ConvertToValue(string fieldName, object obj);
        object ConvertFromValue(string fieldName, BsonValue value);
        string GetKeyValue(T instance);
    }

    internal class EntityMapping<T> : IEntityMapping<T>
    {
        private readonly KeyMapping keyMapping;
        private readonly PropertyMapping[] properties;
        private readonly EntityMigration<T> migration;

        public string Name { get; }
        public Maybe<Func<T, string>> UniqueProperty { get; }

        public EntityMapping(
            string name,
            PropertyMapping[] properties,
            KeyMapping keyMapping = null,
            EntityMigration<T> migration = null,
            Func<T, string> uniqueProperty = null)
        {
            Name = name;
            UniqueProperty = uniqueProperty.ToMaybe();
            this.properties = properties;
            this.keyMapping = keyMapping;
            this.migration = migration;
        }

        public Maybe<string> GetFieldName(PropertyInfo propertyInfo) => properties.MaybeFirst(p => p.Member == propertyInfo).Select(p => p.Name);
        public Maybe<string> GetFieldName(string propertyName) => properties.MaybeFirst(p => p.Member.Name.LowerFirst() == propertyName.LowerFirst()).Select(p => p.Name);
        public BsonValue ConvertToValue(string fieldName, object obj) => properties.First(p => p.Name == fieldName).ConvertToValue(obj);
        public object ConvertFromValue(string fieldName, BsonValue value) => properties.First(p => p.Name == fieldName).ConvertFromValue(value);
        public string GetKeyValue(T instance) => keyMapping.GetKeyValue(instance);

        public BsonDocument ToEntity(T instance, Func<BsonDocument, ObjectId> keyFactory = null)
        {
            if (instance == null)
                return null;
            migration?.BeforeWrite(instance);
            var entity = new BsonDocument();
            keyMapping?.WriteToEntity(entity, instance, keyFactory);
            foreach (var propertyMapping in properties)
                propertyMapping.WriteToEntity(entity, instance);
            return entity;
        }

        public T FromEntity(BsonDocument entity)
        {
            if (entity == null)
                return default;
            migration?.BeforeRead(entity);
            var instance = Activator.CreateInstance<T>();
            keyMapping?.ReadFromEntity(entity, instance);
            foreach (var propertyMapping in properties)
                propertyMapping.ReadFromEntity(entity, instance);
            migration?.AfterRead(entity, instance);
            return instance;
        }

        object IEntityMapping.FromEntity(BsonDocument entity) => FromEntity(entity);
        BsonDocument IEntityMapping.ToEntity(object instance, Func<BsonDocument, ObjectId> keyFactory) => ToEntity((T)instance, keyFactory);

        public IList<IEntityMigration> FindMigrations()
        {
            var migrations = new List<IEntityMigration>();
            if (migration != null)
                migrations.Add(migration);
            migrations.AddRange(properties.SelectMany(p => p.Converter.FindMigrations()));
            return migrations;
        }
    }

    internal class EntityMapping<T, U> : IEntityMapping<T> where U : T
    {
        public string Name { get; }

        public Maybe<Func<T, string>> UniqueProperty
        {
            get
            {
                return concreteMapping.UniqueProperty
                    .Select(p =>
                    {
                        string unique(T o) => p(toConcreteFunc(o));
                        return (Func<T, string>)unique;
                    });
            }
        }

        private readonly IEntityMapping<U> concreteMapping;
        private readonly Func<T, U> toConcreteFunc;

        public EntityMapping(string name, IEntityMapping<U> concreteMapping, Func<T, U> toConcreteFunc)
        {
            Name = name;
            this.concreteMapping = concreteMapping;
            this.toConcreteFunc = toConcreteFunc;
        }

        public Maybe<string> GetFieldName(PropertyInfo propertyInfo) => concreteMapping.GetFieldName(propertyInfo.Name);
        public Maybe<string> GetFieldName(string propertyName) => concreteMapping.GetFieldName(propertyName);
        public BsonValue ConvertToValue(string fieldName, object obj) => concreteMapping.ConvertToValue(fieldName, obj);
        public object ConvertFromValue(string fieldName, BsonValue value) => concreteMapping.ConvertFromValue(fieldName, value);
        public string GetKeyValue(T instance) => concreteMapping.GetKeyValue(toConcreteFunc(instance));
        public BsonDocument ToEntity(T instance, Func<BsonDocument, ObjectId> keyFactory = null) => concreteMapping.ToEntity(toConcreteFunc(instance), keyFactory);
        public T FromEntity(BsonDocument entity) => concreteMapping.FromEntity(entity);

        object IEntityMapping.FromEntity(BsonDocument entity) => FromEntity(entity);
        BsonDocument IEntityMapping.ToEntity(object instance, Func<BsonDocument, ObjectId> keyFactory) => ToEntity((T)instance, keyFactory);

        public IList<IEntityMigration> FindMigrations() => concreteMapping.FindMigrations();
    }

    internal class PropertyMapping
    {
        private readonly bool hasSetter;

        public IValueConverter Converter { get; }
        public PropertyInfo Member { get; }
        public string Name { get; }

        public PropertyMapping(IValueConverter converter, PropertyInfo member, string name)
        {
            Converter = converter ?? throw new ArgumentNullException(nameof(converter));
            Member = member ?? throw new ArgumentNullException(nameof(member));
            Name = name ?? member.Name.LowerFirst();
            var methodInfo = Member.GetSetMethod(true);
            hasSetter = methodInfo != null && (methodInfo.IsPublic || methodInfo.IsPrivate);
        }

        public BsonValue ConvertToValue(object obj) => Converter.ToValue(obj);
        public object ConvertFromValue(BsonValue value) => Converter.FromValue(value);

        public virtual void ReadFromEntity(BsonDocument entity, object instance)
        {
            if (hasSetter)
                Member.SetValue(instance, Converter.FromValue(entity.GetValue(Name, BsonNull.Value)));
        }

        public virtual void WriteToEntity(BsonDocument entity, object instance) => entity[Name] = Converter.ToValue(Member.GetValue(instance)) ?? BsonNull.Value;
    }

    internal class KeyMapping
    {
        public static bool IsKeyEmpty(string key)
        {
            return string.IsNullOrWhiteSpace(key);
        }

        public PropertyInfo Member { get; }

        public KeyMapping(PropertyInfo member)
        {
            Member = member ?? throw new ArgumentNullException(nameof(member));
        }

        public void ReadFromEntity(BsonDocument entity, object instance)
        {
            var key = entity["_id"];
            Member.SetValue(instance, key.ToString());
        }

        public void WriteToEntity(BsonDocument entity, object instance, Func<BsonDocument, ObjectId> keyFactory)
        {
            var key = GetKeyValue(instance);
            entity["_id"] = IsKeyEmpty(key) ? keyFactory(entity) : new ObjectId(key);
        }

        public string GetKeyValue(object instance)
        {
            return (string)Member.GetValue(instance);
        }
    }
}
