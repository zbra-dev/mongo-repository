namespace Mongo.Repository
{
    public class UniqueConstraintException : PersistenceException
    {
        public UniqueConstraintException() : base("Entity already exists")
        {
        }
    }
}
