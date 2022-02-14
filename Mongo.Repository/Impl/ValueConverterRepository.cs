using MongoDB.Bson;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ZBRA.Commons;

namespace Mongo.Repository.Impl
{
    internal class ValueConverterRepository
    {
        private static readonly Dictionary<Type, DelegateValueConverter> basicConverterMap = new[]
        {
            new DelegateValueConverter(typeof(int), v => (int)v, o => (int)o),
            new DelegateValueConverter(typeof(int?), v => (int?)v, o => (int?)o),
            new DelegateValueConverter(typeof(long), v => (long)v, o => (long)o),
            new DelegateValueConverter(typeof(long?), v => (long?)v, o => (long?)o),
            new DelegateValueConverter(typeof(double), v => (double)v, o => (double)o),
            new DelegateValueConverter(typeof(double?), v => (double?)v, o => (double?)o),
            new DelegateValueConverter(typeof(bool), v => (bool)v, o => (bool)o),
            new DelegateValueConverter(typeof(bool?), v => (bool?)v, o => (bool?)o),
            new DelegateValueConverter(typeof(byte), v => (byte)v, o => (byte)o),
            new DelegateValueConverter(typeof(byte?), v => (byte?)v, o => (byte?)o),
            new DelegateValueConverter(typeof(string), v => (string)v, o => (string)o),
            new DelegateValueConverter(typeof(DateTime), v => (DateTime)v, o => EnforceUtc((DateTime)o)),
            new DelegateValueConverter(typeof(DateTime?), v => (DateTime?)v, o => EnforceUtc((DateTime?)o)),
        }.ToDictionary(c => c.Type);

        private static DateTime EnforceUtc(DateTime dt)
        {
            if (dt.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Dates must be UTC");
            return dt;
        }

        private static DateTime? EnforceUtc(DateTime? dt)
        {
            if (dt.HasValue)
                EnforceUtc(dt.Value);
            return dt;
        }

        private readonly Mappings mappings;

        public ValueConverterRepository(Mappings mappings)
        {
            this.mappings = mappings;
        }

        public IValueConverter GetConverter<P, Q>(Type type, Func<Q, P> fromFunc, Func<P, Q> toFunc)
        {
            var innerConverter = GetConverter(typeof(Q));
            return new CompositeConverter<P, Q>(type, fromFunc, toFunc, innerConverter);
        }

        public IValueConverter GetConverter(Type type)
        {
            if (type == typeof(object))
                throw new ArgumentException($"Can't convert to <object>");
            if (type == typeof(float) || type == typeof(float?))
                throw new ArgumentException($"Float is not supported, use double instead");

            if (TryMaybeInnerType(type, out var maybeType))
            {
                return new MaybeConverter(type, GetConverter(maybeType));
            }
            if (basicConverterMap.TryGetValue(type, out var basicConverter))
            {
                return basicConverter;
            }
            if (TryListOrArrayInnerType(type, out var innerType))
            {
                return new ArrayValueConverter(type, GetConverter(innerType));
            }
            if (type.IsEnum)
            {
                return new EnumConverter(type);
            }
            if (type == typeof(Dictionary<string, object>) || type == typeof(IDictionary<string, object>))
            {
                return new MapConverter(type);
            }
            if (type.IsClass || type.IsInterface)
            {
                if (!mappings.Contains(type))
                {
                    if (type.FullName.StartsWith("System.Collections"))
                        throw new ArgumentException($"Type {type} not supported for value conversion");
                    throw new EntityMappingNotFoundException(type);
                }
                return new EntityConverter(type, () => mappings.Get(type));
            }
            throw new ArgumentException($"Could not find converter for type {type}");
        }

        // TODO: STOP USING MAYBE FROM ZBRA.COMMONS
        private bool TryMaybeInnerType(Type type, out Type innerType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Maybe<>))
            {
                innerType = type.GetGenericArguments().Single();
                return true;
            }
            innerType = null;
            return false;
        }

        private bool TryListOrArrayInnerType(Type type, out Type innerType)
        {
            if (type.IsArray)
            {
                innerType = type.GetElementType();
                return true;
            }

            innerType = new[] { type }.Concat(type.GetInterfaces())
                .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IList<>))
                .Select(t => t.GetGenericArguments().Single())
                .FirstOrDefault();
            return innerType != null;
        }

        private class DelegateValueConverter : IValueConverter
        {
            public Type Type { get; }

            private readonly Func<BsonValue, object> fromFunc;
            private readonly Func<object, BsonValue> toFunc;

            public DelegateValueConverter(Type type, Func<BsonValue, object> fromFunc, Func<object, BsonValue> toFunc)
            {
                Type = type ?? throw new ArgumentNullException(nameof(type));
                this.fromFunc = fromFunc ?? throw new ArgumentNullException(nameof(fromFunc));
                this.toFunc = toFunc ?? throw new ArgumentNullException(nameof(toFunc));
            }

            public object FromValue(BsonValue value)
            {
                if (value == null || value.IsBsonNull)
                {
                    if (Type.IsValueType)
                        return Activator.CreateInstance(Type);
                    return null;
                }
                return fromFunc(value);
            }

            public BsonValue ToValue(object obj) => obj == null ? BsonValue.Create(null) : toFunc(obj);
            public IList<IEntityMigration> FindMigrations() => new IEntityMigration[] { };
        }

        private class EnumConverter : IValueConverter
        {
            public Type Type { get; }

            public EnumConverter(Type type)
            {
                Type = type ?? throw new ArgumentNullException(nameof(type));
            }

            public object FromValue(BsonValue value) => Enum.Parse(Type, (string)value);
            public BsonValue ToValue(object obj) => Enum.GetName(Type, obj);
            public IList<IEntityMigration> FindMigrations() => new IEntityMigration[] { };
        }

        private class CompositeConverter<P, Q> : IValueConverter
        {
            public Type Type { get; }

            private readonly Func<Q, P> fromFunc;
            private readonly Func<P, Q> toFunc;
            private readonly IValueConverter innerConverter;

            public CompositeConverter(Type type, Func<Q, P> fromFunc, Func<P, Q> toFunc, IValueConverter innerConverter)
            {
                Type = type ?? throw new ArgumentNullException(nameof(type));
                this.fromFunc = fromFunc ?? throw new ArgumentNullException(nameof(fromFunc));
                this.toFunc = toFunc ?? throw new ArgumentNullException(nameof(toFunc));
                this.innerConverter = innerConverter ?? throw new ArgumentNullException(nameof(innerConverter));
            }

            public object FromValue(BsonValue value) => fromFunc((Q)innerConverter.FromValue(value));
            public BsonValue ToValue(object obj) => innerConverter.ToValue(toFunc((P)obj));
            public IList<IEntityMigration> FindMigrations() => innerConverter.FindMigrations();
        }

        private class MaybeConverter : IValueConverter
        {
            public Type Type { get; }
            private readonly PropertyInfo hasValueProperty;
            private readonly PropertyInfo valueProperty;
            private readonly ConstructorInfo constructor;

            private readonly IValueConverter innerConverter;

            public MaybeConverter(Type type, IValueConverter innerConverter)
            {
                Type = type ?? throw new ArgumentNullException(nameof(type));
                this.innerConverter = innerConverter ?? throw new ArgumentNullException(nameof(innerConverter));
                hasValueProperty = type.GetProperty("HasValue");
                valueProperty = type.GetProperty("Value");

                constructor = Type.GetConstructor(
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new[] { valueProperty.PropertyType, typeof(bool) },
                    null);
            }

            private bool MaybeHasValue(object obj)
            {
                return (bool)hasValueProperty.GetValue(obj);
            }

            private object MaybeValue(object obj)
            {
                return valueProperty.GetValue(obj);
            }

            public object FromValue(BsonValue value)
            {
                return value == null || value.IsBsonNull
                    ? constructor.Invoke(new[] { (object)null, false })
                    : constructor.Invoke(new[] { innerConverter.FromValue(value), true });
            }

            public BsonValue ToValue(object obj)
            {
                if (!MaybeHasValue(obj))
                    return BsonValue.Create(null);
                return innerConverter.ToValue(MaybeValue(obj));
            }

            public IList<IEntityMigration> FindMigrations() => innerConverter.FindMigrations();
        }

        private class ArrayValueConverter : IValueConverter
        {
            public Type Type { get; }

            private readonly IValueConverter innerConverter;

            public ArrayValueConverter(Type type, IValueConverter innerConverter)
            {
                Type = type ?? throw new ArgumentNullException(nameof(type));
                this.innerConverter = innerConverter ?? throw new ArgumentNullException(nameof(innerConverter));
            }

            public object FromValue(BsonValue value)
            {
                if (value == null || value.IsBsonNull)
                    return null;

                var values = value.AsBsonArray;
                var array = Array.CreateInstance(innerConverter.Type, values.Count);
                for (var i = 0; i < values.Count; ++i)
                    array.SetValue(innerConverter.FromValue(values[i]), i);
                return array;
            }

            public BsonValue ToValue(object obj)
            {
                if (obj == null)
                    return BsonValue.Create(null);

                var array = new BsonArray();
                foreach (var value in (ICollection)obj)
                    array.Add(innerConverter.ToValue(value));
                return array;
            }
            public IList<IEntityMigration> FindMigrations() => innerConverter.FindMigrations();
        }

        private class EntityConverter : IValueConverter
        {
            public Type Type { get; }

            private readonly Lazy<IEntityMapping> mapping;

            public EntityConverter(Type type, Func<IEntityMapping> mappingFactory)
            {
                Type = type ?? throw new ArgumentNullException(nameof(type));
                mapping = new Lazy<IEntityMapping>(mappingFactory);
            }

            public object FromValue(BsonValue value) => value.IsBsonNull ? null : mapping.Value.FromEntity(value?.AsBsonDocument);
            public BsonValue ToValue(object obj) => mapping.Value.ToEntity(obj);
            public IList<IEntityMigration> FindMigrations() => mapping.Value.FindMigrations();
        }

        private class MapConverter : IValueConverter
        {
            public Type Type { get; }

            public MapConverter(Type type)
            {
                Type = type;
            }

            public object FromValue(BsonValue value)
            {
                if (value == null || value.IsBsonNull)
                    return null;
                return value.AsBsonDocument.ToDictionary();
            }

            public BsonValue ToValue(object obj)
            {
                if (obj == null)
                    return BsonValue.Create(null);

                var map = (IDictionary<string, object>)obj;
                var entity = new BsonDocument();
                var supportedValueTypes = new[] { typeof(string), typeof(long), typeof(int), typeof(bool) }.ToHashSet();
                foreach (var pair in map)
                {
                    BsonValue value;
                    if (pair.Value is JsonElement jsonElement)
                    {
                        value = jsonElement.ValueKind switch
                        {
                            JsonValueKind.String => jsonElement.GetString(),
                            JsonValueKind.Number => jsonElement.GetInt64(),
                            JsonValueKind.True => jsonElement.GetBoolean(),
                            JsonValueKind.False => jsonElement.GetBoolean(),
                            JsonValueKind.Null => BsonValue.Create(null),
                            _ => throw new ArgumentException($"Json value kind {jsonElement.ValueKind} not supported"),
                        };
                    }
                    else
                    {
                        if (pair.Value == null)
                        {
                            value = BsonValue.Create(null);
                        }
                        else if (!supportedValueTypes.Contains(pair.Value.GetType()))
                        {
                            throw new ArgumentException($"Value type {pair.Value.GetType().Name} not supported");
                        }
                        else if (basicConverterMap.TryGetValue(pair.Value.GetType(), out var converter))
                        {
                            value = converter.ToValue(pair.Value);
                        }
                        else
                        {
                            throw new ArgumentException($"Cannot convert type {pair.Value.GetType()} to a Value object");
                        }
                    }
                    entity[pair.Key] = value;
                }
                return entity;
            }

            public IList<IEntityMigration> FindMigrations() => new IEntityMigration[] { };
        }
    }
}