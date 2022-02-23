using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ZBRA.Maybe;

namespace ZBRA.Mongo.Repository
{
    public interface IRepository<T>
    {
        ResultPage<T> Query(IFilter<T> filter, ISessionHandle session = null);
        ResultPage<T> Query<TP>(Expression<Func<T, TP>> expression, object value, ISessionHandle session = null);
        ResultPage<T> QueryAll(int? limit = null, int? skip = null, ISessionHandle session = null);
        Maybe<T> FindById(string id, ISessionHandle session = null);
        string Insert(T instance, ISessionHandle session = null);
        string[] Insert(T[] instances, ISessionHandle session = null);
        void Update(T[] instances, ISessionHandle session = null);
        void Update(T instance, ISessionHandle session = null);
        Maybe<string> Upsert(T instance, ISessionHandle session = null);
        string[] Upsert(T[] instances, ISessionHandle session = null);
        void Delete(T[] instances, ISessionHandle session = null);
        void Delete(T instance, ISessionHandle session = null);
        void Delete(string[] ids, ISessionHandle session = null);
        void Delete(string id, ISessionHandle session = null);

        Task<ResultPage<T>> QueryAsync(IFilter<T> filter, ISessionHandle session = null);
        Task<ResultPage<T>> QueryAsync<TP>(Expression<Func<T, TP>> expression, object value, ISessionHandle session = null);
        Task<ResultPage<T>> QueryAllAsync(int? limit = null, int? skip = null, ISessionHandle session = null);
        Task<Maybe<T>> FindByIdAsync(string id, ISessionHandle session = null);
        Task<string> InsertAsync(T instance, ISessionHandle session = null);
        Task<string[]> InsertAsync(T[] instances, ISessionHandle session = null);
        Task UpdateAsync(T[] instances, ISessionHandle session = null);
        Task UpdateAsync(T instance, ISessionHandle session = null);
        Task<Maybe<string>> UpsertAsync(T instance, ISessionHandle session = null);
        Task<string[]> UpsertAsync(T[] instances, ISessionHandle session = null);
        Task DeleteAsync(T[] instances, ISessionHandle session = null);
        Task DeleteAsync(T instance, ISessionHandle session = null);
        Task DeleteAsync(string id, ISessionHandle session = null);
        Task DeleteAsync(string[] ids, ISessionHandle session = null);
        Task<ISessionHandle> StartSessionAsync();
        ISessionHandle StartSession();
    }
}