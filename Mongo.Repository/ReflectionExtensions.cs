using System;
using System.Linq.Expressions;
using System.Reflection;

namespace ZBRA.Mongo.Repository
{
    internal static class ReflectionExtensions
    {
        public static void SetPropertyValue(this object obj, string propertyName, object value)
        {
            var type = obj.GetType();
            var property = type.GetProperty(propertyName);
            if (property == null)
                throw new ArgumentException(nameof(propertyName));
            if (value.GetType() == property.PropertyType)
            {
                property.SetValue(obj, value);
            }
            else if (value.GetType() == typeof(string))
            {
                property.SetValue(obj, Convert.ChangeType(value, property.PropertyType));
            }
            else
            {
                throw new ArgumentException($"Value [{value}] could not be converted to type [{property.PropertyType}]");
            }
        }

        public static void CallMethod(this object obj, string methodName, params object[] arguments)
        {
            var type = obj.GetType();
            var method = type.GetMethod(methodName);
            method.Invoke(obj, arguments);
        }

        public static PropertyInfo ExtractPropertyInfo<T, P>(this Expression<Func<T, P>> expression)
        {
            var exp = (LambdaExpression)expression;
            var prop = ((MemberExpression)exp.Body).Member;
            if (prop is PropertyInfo propertyInfo)
                return propertyInfo;
            throw new ArgumentException("Expression must be for a property");
        }
    }
}
