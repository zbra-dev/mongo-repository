using System;
using System.Linq.Expressions;

namespace ZBRA.Mongo.Repository
{
    public interface IFieldResolver<T>
    {
        string FieldName<P>(Expression<Func<T, P>> expression);
        string FieldName(string propertyName);
    }
}
