using Google.Cloud.Mongo.V1;
using System;
using System.Collections.Generic;

namespace Mongo.Repository.Impl
{
    internal class ExcludeFromIndexesDecorator : IValueConverter
    {
        private readonly IValueConverter innerConverter;

        public ExcludeFromIndexesDecorator(IValueConverter innerConverter)
        {
            this.innerConverter = innerConverter ?? throw new ArgumentNullException(nameof(innerConverter));
        }

        public Type Type => innerConverter.Type;
        public IList<IEntityMigration> FindMigrations() => innerConverter.FindMigrations();
        public object FromValue(Value value) => innerConverter.FromValue(value);

        public Value ToValue(object obj)
        {
            var value = innerConverter.ToValue(obj);

            value.ExcludeFromIndexes = true;

            return value;
        }
    }
}
