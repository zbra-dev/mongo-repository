using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ZBRA.Maybe;

namespace ZBRA.Mongo.Repository
{
    public interface IRepository<T>
    {
        ResultPage<T> Query(IFilter<T> filter, ISessionHandle session = null);
        ResultPage<T> Query<P>(Expression<Func<T, P>> expression, object value, ISessionHandle session = null);
        ResultPage<T> QueryAll();
        ResultPage<T> QueryAll(int? limit = null, int? skip = null, ISessionHandle session = null);
        Maybe<T> FindById(string id, ISessionHandle session = null);
        string Insert(T instance, ISessionHandle session = null);
        string[] Insert(T[] instances, ISessionHandle session = null);
        void Update(params T[] instances);
        Maybe<string> Upsert(T instance);
        string[] Upsert(params T[] instances);
        void Delete(params T[] instances);
        void Delete(params string[] ids);

        Task<ResultPage<T>> QueryAsync(IFilter<T> filter, ISessionHandle session = null);
        Task<ResultPage<T>> QueryAsync<P>(Expression<Func<T, P>> expression, object value, ISessionHandle session = null);
        Task<ResultPage<T>> QueryAllAsync();
        Task<ResultPage<T>> QueryAllAsync(int? limit = null, int? skip = null, ISessionHandle session = null);
        Task<Maybe<T>> FindByIdAsync(string id, ISessionHandle session = null);
        Task<string> InsertAsync(T instance, ISessionHandle session = null);
        Task<string[]> InsertAsync(T[] instances, ISessionHandle session = null);
        Task UpdateAsync(params T[] instances);
        Task<Maybe<string>> UpsertAsync(T instance);
        Task<string[]> UpsertAsync(params T[] instances);
        Task DeleteAsync(params T[] instances);
        Task DeleteAsync(params string[] ids);
        Task<ISessionHandle> StartSessionAsync();
        ISessionHandle StartSession();
    }
}