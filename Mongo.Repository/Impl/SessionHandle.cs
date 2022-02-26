using System.Threading.Tasks;
using MongoDB.Driver;

namespace ZBRA.Mongo.Repository.Impl
{
    internal class SessionHandle : ISessionHandle
    {
        public IClientSessionHandle InnerSession { get; }

        public SessionHandle(IClientSessionHandle session)
        {
            InnerSession = session;
        }

        public void StartTransaction()
        {
            InnerSession.StartTransaction();
        }

        public async Task CommitTransactionAsync()
        {
            await InnerSession.CommitTransactionAsync();
        }

        public void CommitTransaction()
        {
            InnerSession.CommitTransaction();
        }

        public async Task AbortTransactionAsync()
        {
            await InnerSession.AbortTransactionAsync();
        }

        public void AbortTransaction()
        {
            InnerSession.AbortTransaction();
        }
    }
}