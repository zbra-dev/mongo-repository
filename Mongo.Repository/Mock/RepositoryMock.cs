using Mongo.Repository.Impl;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ZBRA.Maybe;

namespace Mongo.Repository.Mock
{
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

    /// <summary>Class <c>RepositoryMock<></c> is meant to be used in very simple test cases.
    /// Overall its usage is discouraged and it will be removed in future versions.
    /// Instead tests should be done using a DatastoreFixture.
    /// </summary>
    public class RepositoryMock<T> : IRepository<T>
    {
        private readonly Dictionary<string, BsonDocument> map = new Dictionary<string, BsonDocument>();
        private readonly IEntityMapping<T> mapping;
        private readonly Random random = new Random();
        private readonly Dictionary<Type, Func<IFilter<T>, ResultPage<T>, ResultPage<T>>> filterMap = new Dictionary<Type, Func<IFilter<T>, ResultPage<T>, ResultPage<T>>>();

        public RepositoryMock(Mappings mappings)
        {
            mapping = mappings.Get<T>();
            if (mapping.UniqueProperty.HasValue)
                throw new ArgumentException("RepositoryMock does not support mappings with unique constraint");
        }

        public RepositoryMock(Mappings mappings, T[] instances)
            : this(mappings)
        {
            Insert(instances);
        }

        public void Clear() => map.Clear();
        public void AddFilterFunc<F>(Func<IFilter<T>, ResultPage<T>, ResultPage<T>> filterFunc) where F : IFilter<T> => filterMap[typeof(F)] = filterFunc;
        
        private ObjectId CreateId(BsonDocument document)
        {
            return new ObjectId(Guid.NewGuid().ToString("N"));
        }

        private long GenerateKey()
        {
            var key = random.Next();
            if (!map.ContainsKey(key.ToString()))
                return key;
            return GenerateKey();
        }

        public async Task<string[]> InsertAsync(params T[] instances)
        {
            return instances
                .Select(i =>
                {
                    var entity = mapping.ToEntity(i, CreateId);
                    var key = entity["_id"].ToString();
                    map.Add(key, entity);
                    return key;
                }).ToArray();
        }

        public async Task DeleteAsync(params T[] instances)
        {
            foreach (var instance in instances)
                map.Remove(mapping.GetKeyValue(instance));
        }

        public async Task DeleteAsync(params string[] ids)
        {
            foreach (var id in ids)
                map.Remove(id);
        }

        public async Task<ResultPage<T>> QueryAsync<P>(Expression<Func<T, P>> expression, object value)
        {
            var property = expression.ExtractPropertyInfo();
            var result = await QueryAllAsync();
            return new ResultPage<T>(result.Entities.Where(v => Equals(property.GetValue(v), value)), result.HasMoreResults);
        }

        public async Task UpdateAsync(params T[] instances)
        {
            foreach (var instance in instances)
            {
                var key = mapping.GetKeyValue(instance);
                map[key] = mapping.ToEntity(instance);
            }
        }

        public async Task<Maybe<string>[]> UpsertAsync(params T[] instances)
        {
            var result = new List<Maybe<string>>();
            foreach (var instance in instances)
            {
                var key = mapping.GetKeyValue(instance);
                if (string.IsNullOrEmpty(key))
                {
                    result.Add((await InsertAsync(instance)).ToMaybe());
                }
                else
                {
                    await UpdateAsync(instance);
                    result.Add(Maybe<string>.Nothing);
                }
            }
            return result.ToArray();
        }

        public async Task<ResultPage<T>> QueryAsync(IFilter<T> filter)
        {
            if (!filterMap.TryGetValue(filter.GetType(), out var filterFunc))
                throw new ArgumentException($"Filter [{filter.GetType()}] hasn't been mapped");
            var result = await QueryAllAsync();
            return filterFunc(filter, result);
        }

        public async Task<Maybe<T>> FindByIdAsync(string id)
        {
            return map.MaybeGet(id).Select(e => mapping.FromEntity(e));
        }

        public async Task<ResultPage<T>> QueryAllAsync() => new ResultPage<T>(map.Values.Select(i => mapping.FromEntity(i)));
        public Task<ResultPage<T>> QueryAllAsync(int? limit = null, int? skup = null) => throw new NotImplementedException();
        public async Task<string> InsertAsync(T instance) => (await InsertAsync(new[] { instance })).First();
        public async Task<Maybe<string>> UpsertAsync(T instance) => (await UpsertAsync(new[] { instance })).First();

        public Maybe<T> FindById(string id) => FindByIdAsync(id).Result;
        public ResultPage<T> Query(IFilter<T> filter) => QueryAsync(filter).Result;
        public ResultPage<T> Query<P>(Expression<Func<T, P>> expression, object value) => QueryAsync(expression, value).Result;
        public ResultPage<T> QueryAll() => QueryAllAsync().Result;
        public ResultPage<T> QueryAll(int? limit = null, int? skip = null) => QueryAllAsync(limit, skip).Result;
        public string Insert(T instance) => InsertAsync(instance).Result;
        public string[] Insert(params T[] instances) => InsertAsync(instances).Result;
        public void Update(params T[] instances) => UpdateAsync(instances).Wait();
        public Maybe<string> Upsert(T instance) => UpsertAsync(instance).Result;
        public Maybe<string>[] Upsert(params T[] instances) => UpsertAsync(instances).Result;
        public void Delete(params T[] instances) => DeleteAsync(instances).Wait();
        public void Delete(params string[] ids) => DeleteAsync(ids).Wait();
    }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
}
