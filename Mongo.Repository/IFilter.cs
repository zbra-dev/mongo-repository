using MongoDB.Bson;
using MongoDB.Driver;

namespace Mongo.Repository
{
    public interface IFilter<T>
    {
        FilterDefinition<BsonDocument> CreateFilter(IFieldResolver<T> resolver);
        SortDefinition<BsonDocument> CreateSort(IFieldResolver<T> resolver) => null;
        int? Take { get; }
        int? Skip { get; }
    }
}
