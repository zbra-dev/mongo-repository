using System.Collections.Generic;
using System.Linq;

namespace Mongo.Repository
{
    public class ResultPage<T>
    {
        public ResultPage(IEnumerable<T> entities, bool hasMoreResults = false)
        {
            Entities = entities.ToArray();
            HasMoreResults = hasMoreResults && Entities.Length > 0;
        }

        public T[] Entities { get; }
        public bool HasMoreResults { get; } = false;
    }
}
