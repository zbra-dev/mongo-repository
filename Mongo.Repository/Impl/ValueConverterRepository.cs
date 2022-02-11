using Google.Cloud.Mongo.V1;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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

            private Func<Value, object> fromFunc;
            private Func<object, Value> toFunc;

            public DelegateValueConverter(Type type, Func<Value, object> fromFunc, Func<object, Value> toFunc)
            {
                Type = type ?? throw new ArgumentNullException(nameof(type));
                this.fromFunc = fromFunc ?? throw new ArgumentNullException(nameof(fromFunc));
                this.toFunc = toFunc ?? throw new ArgumentNullException(nameof(toFunc));
            }

            public object FromValue(Value value)
            {
                if (value == null || value.IsNull)
                {
                    if (Type.IsValueType)
                        return Activator.CreateInstance(Type);
                    return null;
                }
                return fromFunc(value);
            }

            public Value ToValue(object obj) => obj == null ? Value.ForNull() : toFunc(obj);
            public IList<IEntityMigration> FindMigrations() => new IEntityMigration[] { };
        }

        private class EnumConverter : IValueConverter
        {
            public Type Type { get; }

            public EnumConverter(Type type)
            {
                Type = type ?? throw new ArgumentNullException(nameof(type));
            }

            public object FromValue(Value value) => Enum.Parse(Type, (string)value);
            public Value ToValue(object obj) => Enum.GetName(Type, obj);
            public IList<IEntityMigration> FindMigrations() => new IEntityMigration[] { };
        }

        private class CompositeConverter<P, Q> : IValueConverter
        {
            public Type Type { get; }

            private Func<Q, P> fromFunc;
            private Func<P, Q> toFunc;
            private IValueConverter innerConverter;

            public CompositeConverter(Type type, Func<Q, P> fromFunc, Func<P, Q> toFunc, IValueConverter innerConverter)
            {
                Type = type ?? throw new ArgumentNullException(nameof(type));
                this.fromFunc = fromFunc ?? throw new ArgumentNullException(nameof(fromFunc));
                this.toFunc = toFunc ?? throw new ArgumentNullException(nameof(toFunc));
                this.innerConverter = innerConverter ?? throw new ArgumentNullException(nameof(innerConverter));
            }

            public object FromValue(Value value) => fromFunc((Q)innerConverter.FromValue(value));
            public Value ToValue(object obj) => innerConverter.ToValue(toFunc((P)obj));
            public IList<IEntityMigration> FindMigrations() => innerConverter.FindMigrations();
        }

        private class MaybeConverter : IValueConverter
        {
            public Type Type { get; }
            private PropertyInfo hasValueProperty;
            private PropertyInfo valueProperty;
            private ConstructorInfo constructor;

            private IValueConverter innerConverter;

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

            public object FromValue(Value value)
            {
                return value == null || value.IsNull
                    ? constructor.Invoke(new[] { (object)null, false })
                    : constructor.Invoke(new[] { innerConverter.FromValue(value), true });
            }

            public Value ToValue(object obj)
            {
                if (!MaybeHasValue(obj))
                    return Value.ForNull();
                return innerConverter.ToValue(MaybeValue(obj));
            }

            public IList<IEntityMigration> FindMigrations() => innerConverter.FindMigrations();
        }

        private class ArrayValueConverter : IValueConverter
        {
            public Type Type { get; }

            private IValueConverter innerConverter;

            public ArrayValueConverter(Type type, IValueConverter innerConverter)
            {
                Type = type ?? throw new ArgumentNullException(nameof(type));
                this.innerConverter = innerConverter ?? throw new ArgumentNullException(nameof(innerConverter));
            }

            public object FromValue(Value value)
            {
                if (value == null || value.IsNull)
                    return null;

                var values = value.ArrayValue?.Values;
                if (values == null)
                    return null;

                var array = Array.CreateInstance(innerConverter.Type, values.Count);
                for (var i = 0; i < values.Count; ++i)
                    array.SetValue(innerConverter.FromValue(values[i]), i);
                return array;
            }

            public Value ToValue(object obj)
            {
                if (obj == null)
                    return Value.ForNull();

                var array = new ArrayValue();
                foreach (var value in (ICollection)obj)
                    array.Values.Add(innerConverter.ToValue(value));
                return array;
            }

            public IList<IEntityMigration> FindMigrations() => innerConverter.FindMigrations();
        }

        private class EntityConverter : IValueConverter
        {
            public Type Type { get; }

            private Lazy<IEntityMapping> mapping;

            public EntityConverter(Type type, Func<IEntityMapping> mappingFactory)
            {
                Type = type ?? throw new ArgumentNullException(nameof(type));
                mapping = new Lazy<IEntityMapping>(mappingFactory);
            }

            public object FromValue(Value value) => mapping.Value.FromEntity(value?.EntityValue);
            public Value ToValue(object obj) => mapping.Value.ToEntity(obj);
            public IList<IEntityMigration> FindMigrations() => mapping.Value.FindMigrations();
        }

        private class MapConverter : IValueConverter
        {
            public Type Type { get; }

            public MapConverter(Type type)
            {
                Type = type;
            }

            public object FromValue(Value value)
            {
                if (value == null || value.IsNull)
                    return null;

                var map = new Dictionary<string, object>();
                foreach (var property in value.EntityValue.Properties)
                {
                    var entityValue =
                        property.Value.ValueTypeCase == Value.ValueTypeOneofCase.BooleanValue ? (bool)property.Value :
                        property.Value.ValueTypeCase == Value.ValueTypeOneofCase.StringValue ? (string)property.Value :
                        property.Value.ValueTypeCase == Value.ValueTypeOneofCase.IntegerValue ? (long)property.Value :
                        property.Value.ValueTypeCase == Value.ValueTypeOneofCase.NullValue ? (object)null :
                        throw new ArgumentException($"Entity value type {property.Value.ValueTypeCase} not supported");

                    map[property.Key] = entityValue;
                }
                return map;
            }

            public Value ToValue(object obj)
            {
                if (obj == null)
                    return Value.ForNull();

                var map = (IDictionary<string, object>)obj;
                var entity = new Entity();
                var supportedValueTypes = new[] { typeof(string), typeof(long), typeof(int), typeof(bool) }.ToHashSet();
                foreach (var pair in map)
                {
                    Value value;
                    if (pair.Value is JsonElement jsonElement)
                    {
                        value =
                            jsonElement.ValueKind == JsonValueKind.String ? jsonElement.GetString() :
                            jsonElement.ValueKind == JsonValueKind.Null ? Value.ForNull() :
                            jsonElement.ValueKind == JsonValueKind.Number ? jsonElement.GetInt64() :
                            jsonElement.ValueKind == JsonValueKind.True ? jsonElement.GetBoolean() :
                            jsonElement.ValueKind == JsonValueKind.False ? (Value)jsonElement.GetBoolean() :
                            throw new ArgumentException($"Json value kind {jsonElement.ValueKind} not supported");
                    }
                    else
                    {
                        if (pair.Value == null)
                        {
                            value = Value.ForNull();
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