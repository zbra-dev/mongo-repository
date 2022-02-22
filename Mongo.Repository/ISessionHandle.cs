using System.Threading.Tasks;

namespace ZBRA.Mongo.Repository
{
    public interface ISessionHandle
    {
        void StartTransaction();

        Task CommitTransactionAsync();

        void CommitTransaction();

        Task AbortTransactionAsync();

        void AbortTransaction();
    }
}