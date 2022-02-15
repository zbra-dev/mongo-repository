using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mongo.Repository
{
    public static class PersistenceExtensions
    {
        public static async Task<IEnumerable<BsonDocument>> QueryAllAsync(this IMongoDatabase db)
        {
            return (await db.ListCollectionsAsync()).ToEnumerable();
        }
    }
}