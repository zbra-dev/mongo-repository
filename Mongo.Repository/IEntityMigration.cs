using MongoDB.Bson;

namespace Mongo.Repository
{
    public interface IEntityMigration
    {
        public int Version { get; }
    }

    public abstract class EntityMigration<T> : IEntityMigration
    {
        public virtual int Version => 1;

        public virtual void BeforeWrite(T instance)
        {

        }

        public virtual void BeforeRead(BsonDocument entity)
        {

        }

        public virtual void AfterRead(BsonDocument entity, T instance)
        {

        }
    }
}
