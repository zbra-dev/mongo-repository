using Google.Cloud.Mongo.V1;

namespace Mongo.Repository
{
    public interface IFilter<T>
    {
        void ApplyTo(Query query, IFieldResolver<T> resolver);
    }
}
