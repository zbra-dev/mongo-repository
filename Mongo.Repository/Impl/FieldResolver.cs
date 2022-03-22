using System;
using System.Linq.Expressions;

namespace ZBRA.Mongo.Repository.Impl
{
    internal class FieldResolver<T> : IFieldResolver<T>
    {
        private readonly IEntityMapping<T> mapping;

        public FieldResolver(IEntityMapping<T> mapping)
        {
            this.mapping = mapping;
        }

        public string FieldName<TP>(Expression<Func<T, TP>> expression)
        {
            var property = expression.ExtractPropertyInfo();
            return mapping.GetFieldName(property)
                .OrThrow(() => new ArgumentException($"Property [{property.Name}] not found"));
        }

        public string FieldName(string propertyName)
        {
            return mapping.GetFieldName(propertyName)
                .OrThrow(() => new ArgumentException($"Property [{propertyName}] not found"));
        }
    }
}
