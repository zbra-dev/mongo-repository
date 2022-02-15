using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mongo.Repository
{
    public static class PersistenceExtensions
    {
        public static async Task<IEnumerable<BsonDocument>> QueryAllAsync(this IMongoDatabase db)
        {
            var collectionNames = (await db.ListCollectionNamesAsync()).ToList();

            var allDocouments = new List<BsonDocument>();
            foreach (var collectionName in collectionNames)
            {
                var collection = db.GetCollection<BsonDocument>(collectionName);
                var documents = await (await collection.FindAsync(FilterDefinition<BsonDocument>.Empty)).ToListAsync();
                allDocouments.AddRange(documents);
            }

            return allDocouments;
        }
    }
}