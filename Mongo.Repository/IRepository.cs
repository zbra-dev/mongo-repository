using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ZBRA.Maybe;

namespace Mongo.Repository
{
    public interface IRepository<T>
    {
        ResultPage<T> Query(IFilter<T> filter);
        ResultPage<T> Query<P>(Expression<Func<T, P>> expression, object value);
        ResultPage<T> QueryAll();
        ResultPage<T> QueryAll(int? limit = null, int? skip = null);
        Maybe<T> FindById(string id);
        string Insert(T instance);
        string[] Insert(params T[] instances);
        void Update(params T[] instances);
        Maybe<string> Upsert(T instance);
        Maybe<string>[] Upsert(params T[] instances);
        void Delete(params T[] instances);
        void Delete(params string[] ids);

        Task<ResultPage<T>> QueryAsync(IFilter<T> filter);
        Task<ResultPage<T>> QueryAsync<P>(Expression<Func<T, P>> expression, object value);
        Task<ResultPage<T>> QueryAllAsync();
        Task<ResultPage<T>> QueryAllAsync(int? limit = null, int? skip = null);
        Task<Maybe<T>> FindByIdAsync(string id);
        Task<string> InsertAsync(T instance);
        Task<string[]> InsertAsync(params T[] instances);
        Task UpdateAsync(params T[] instances);
        Task<Maybe<string>> UpsertAsync(T instance);
        Task<Maybe<string>[]> UpsertAsync(params T[] instances);
        Task DeleteAsync(params T[] instances);
        Task DeleteAsync(params string[] ids);
    }
}