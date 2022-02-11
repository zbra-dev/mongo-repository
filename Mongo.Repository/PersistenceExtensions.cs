using Google.Cloud.Mongo.V1;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mongo.Repository
{
    public static class PersistenceExtensions
    {
        public static async Task<IEnumerable<Entity>> QueryAllAsync(this DatastoreDb db)
        {
            var query = new Query();
            var result = await db.RunQueryAsync(query);
            return result.Entities;
        }
    }
}