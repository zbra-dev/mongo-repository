using Mongo.Repository.Impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using ZBRA.Commons;

namespace Mongo.Repository
{
    public class MappingBuilder<T>
    {
        private static readonly string[] KeyPropertyNames = new[] { "id", "key" };

        private readonly Mappings mappings;
        private readonly ValueConverterRepository valueConverterRepository;
        private readonly string entityName;
        private readonly Dictionary<string, PropertyMapping> propertyMap = new Dictionary<string, PropertyMapping>();
        private readonly HashSet<string> ignoreList = new HashSet<string>();
        private KeyMapping keyMapping = null;
        private Expression<Func<T, string>> uniqueProperty = null;
        private EntityMigration<T> migration = null;

        public MappingBuilder(Mappings mappings, string entityName = null)
        {
            this.mappings = mappings;
            this.valueConverterRepository = new ValueConverterRepository(mappings);
            this.entityName = entityName ?? typeof(T).Name;
        }

        public MappingBuilder<T> Unique(Expression<Func<T, string>> uniqueProperty)
        {
            if (uniqueProperty != null)
                throw new ArgumentException("Unique func already defined");
            this.uniqueProperty = uniqueProperty;
            return this;
        }

        public MappingBuilder<T> Property<P, Q>(Expression<Func<T, P>> expression, Func<Q, P> fromFunc, Func<P, Q> toFunc, string name = null)
        {
            var property = expression.ExtractPropertyInfo();
            var converter = valueConverterRepository.GetConverter(property.PropertyType, fromFunc, toFunc);

            propertyMap.Add(property.Name, new PropertyMapping(converter, property, name));
            return this;
        }

        public MappingBuilder<T> Property<P>(Expression<Func<T, P>> expression, string name = null, bool hasPublicSetter = true)
        {
            AddProperty(expression.ExtractPropertyInfo(), name, hasPublicSetter);
            return this;
        }

        public MappingBuilder<T> Ignore<P>(Expression<Func<T, P>> expression)
        {
            ignoreList.Add(expression.ExtractPropertyInfo().Name);
            return this;
        }

        private void AddProperty(PropertyInfo property, string name = null, bool hasPublicSetter = true)
        {
            var setMethodInfo = property.GetSetMethod(true);
            if (hasPublicSetter && (setMethodInfo == null || !setMethodInfo.IsPublic))
            {
                throw new ArgumentException("Property must have a public set method");
            }

            var converter = valueConverterRepository.GetConverter(property.PropertyType);
            propertyMap.Add(property.Name, new PropertyMapping(converter, property, name));
        }

        public MappingBuilder<T> Key<P>(Expression<Func<T, P>> expression)
        {
            var member = expression.ExtractPropertyInfo();
            if (member.PropertyType != typeof(string))
                throw new ArgumentException("Only <string> is supported for key properties");
            keyMapping = new KeyMapping(member);
            return this;
        }

        public MappingBuilder<T> WithMigrations(EntityMigration<T> migration)
        {
            this.migration = migration;
            return this;
        }

        public MappingBuilder<T> Infer(bool inferUnknownTypes = false)
        {
            var type = typeof(T);
            foreach (var property in type.GetProperties().Where(p => p.CanWrite && p.GetSetMethod(true).IsPublic))
            {
                if (property.PropertyType == typeof(string) && KeyPropertyNames.Contains(property.Name.LowerFirst()))
                {
                    if (keyMapping == null)
                        keyMapping = new KeyMapping(property);
                }
                else if (!propertyMap.ContainsKey(property.Name)
                    && !ignoreList.Contains(property.Name)
                    && keyMapping?.Member.Name != property.Name)
                {
                    try
                    {
                        AddProperty(property);
                    }
                    catch (EntityMappingNotFoundException ex)
                    {
                        if (!inferUnknownTypes)
                            throw;
                        var builderType = typeof(MappingBuilder<>).MakeGenericType(ex.UnknownType);
                        var builder = Activator.CreateInstance(builderType, mappings, null);
                        builder.CallMethod("Infer", true);
                        builder.CallMethod("Build");
                        AddProperty(property);
                    }
                }
            }
            return this;
        }

        public void Build()
        {
            mappings.Add(new EntityMapping<T>(entityName, propertyMap.Values.ToArray(), keyMapping, migration, uniqueProperty));
        }
    }

    public class MappingBuilder<T, U> where U : T
    {
        private readonly Mappings mappings;
        private readonly string entityName;
        private Func<T, U> toConcreteFunc;

        public MappingBuilder(Mappings mappings, string entityName = null)
        {
            this.mappings = mappings;
            this.entityName = entityName;
        }

        public MappingBuilder<T, U> WithConcreteFunc(Func<T, U> toConcreteFunc)
        {
            this.toConcreteFunc = toConcreteFunc;
            return this;
        }

        public MappingBuilder<T, U> Infer(bool inferUnknownTypes = false)
        {
            if (!mappings.Contains<U>())
            {
                if (!inferUnknownTypes)
                    throw new ArgumentException($"Type {typeof(U)} hasn't been mapped");

                mappings.Entity<U>()
                    .Infer(true)
                    .Build();
            }
            return this;
        }

        public void Build()
        {
            if (toConcreteFunc == null)
                throw new ArgumentException("Missing concreteFactory - must call ToConcrete once");
            var concreteMapping = mappings.Get<U>();
            mappings.Add(new EntityMapping<T, U>(entityName ?? concreteMapping.Name, concreteMapping, toConcreteFunc));
        }
    }
}