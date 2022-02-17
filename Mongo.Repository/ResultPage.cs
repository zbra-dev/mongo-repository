namespace Mongo.Repository
{
    public class ResultPage<T>
    {
        public ResultPage(T[] entities, bool hasMoreResults = false)
        {
            Entities = entities;
            HasMoreResults = hasMoreResults;
        }

        public T[] Entities { get; }
        public bool HasMoreResults { get; } = false;
    }
}
