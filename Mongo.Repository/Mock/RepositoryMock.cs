using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Bson;
using ZBRA.Maybe;
using ZBRA.Mongo.Repository.Impl;

namespace ZBRA.Mongo.Repository.Mock
{
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

    /// <summary>Class <c>RepositoryMock&lt;T&gt;</c> is meant to be used in very simple test cases.
    /// Overall its usage is discouraged and it will be removed in future versions.
    /// Instead tests should be done using a MongoDBFixture.
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
            if (mapping.UniqueProperty != null)
                throw new ArgumentException("RepositoryMock does not support mappings with unique constraint");
        }

        public RepositoryMock(Mappings mappings, T[] instances)
            : this(mappings)
        {
            Insert(instances);
        }

        public void Clear() => map.Clear();
        public void AddFilterFunc<TF>(Func<IFilter<T>, ResultPage<T>, ResultPage<T>> filterFunc) where TF : IFilter<T> => filterMap[typeof(TF)] = filterFunc;
        
        private static ObjectId CreateId(BsonDocument document)
        {
            return new ObjectId(Guid.NewGuid().ToString("N"));
        }

        private long GenerateKey()
        {
            var key = random.Next();
            return !map.ContainsKey(key.ToString()) ? key : GenerateKey();
        }

        public async Task<string[]> InsertAsync(T[] instances, ISessionHandle session = null)
        {
            if (session != null)
                throw new NotImplementedException();
            return instances
                .Select(i =>
                {
                    var entity = mapping.ToEntity(i, CreateId);
                    var key = entity["_id"].ToString();
                    map.Add(key, entity);
                    return key;
                }).ToArray();
        }

        public async Task DeleteAsync(T[] instances, ISessionHandle session = null)
        {
            if (session != null)
                throw new NotImplementedException();
            foreach (var instance in instances)
                map.Remove(mapping.GetKeyValue(instance));
        }

        public async Task DeleteAsync(string[] ids, ISessionHandle session = null)
        {
            if (session != null)
                throw new NotImplementedException();
            foreach (var id in ids)
                map.Remove(id);
        }

        public Task<ISessionHandle> StartSessionAsync() => throw new NotImplementedException();

        public ISessionHandle StartSession() => throw new NotImplementedException();

        public async Task<ResultPage<T>> QueryAsync<TP>(Expression<Func<T, TP>> expression, object value, ISessionHandle session = null)
        {
            if (session != null)
                throw new NotImplementedException();
            var property = expression.ExtractPropertyInfo();
            var result = await QueryAllAsync();
            return new ResultPage<T>(result.Entities.Where(v => Equals(property.GetValue(v), value)).ToArray(), result.HasMoreResults);
        }

        public async Task UpdateAsync(T[] instances, ISessionHandle session = null)
        {
            if (session != null)
                throw new NotImplementedException();
            foreach (var instance in instances)
            {
                var key = mapping.GetKeyValue(instance);
                map[key] = mapping.ToEntity(instance);
            }
        }

        public async Task<string[]> UpsertAsync(T[] instances, ISessionHandle session = null)
        {
            if (session != null)
                throw new NotImplementedException();
            var result = new List<string>();
            foreach (var instance in instances)
            {
                var key = mapping.GetKeyValue(instance);
                if (string.IsNullOrEmpty(key))
                {
                    result.Add((await InsertAsync(instance)));
                }
                else
                {
                    await UpdateAsync(instance);
                }
            }
            return result.ToArray();
        }

        public async Task<ResultPage<T>> QueryAsync(IFilter<T> filter, ISessionHandle session = null)
        {
            if (session != null)
                throw new NotImplementedException();
            if (!filterMap.TryGetValue(filter.GetType(), out var filterFunc))
                throw new ArgumentException($"Filter [{filter.GetType()}] hasn't been mapped");
            var result = await QueryAllAsync();
            return filterFunc(filter, result);
        }

        public async Task<ResultPage<T>> QueryAllAsync(int? skip = null, int? take = null, ISessionHandle session = null)
        {
            if (session != null)
                throw new NotImplementedException();
            var result = map.Values.Select(i => mapping.FromEntity(i)).ToArray();
            return new ResultPage<T>(result.Skip(skip ?? 0).Take(take ?? int.MaxValue).ToArray());
        }


        public async Task<Maybe<T>> FindByIdAsync(string id, ISessionHandle session = null)
        {
            if (session != null)
                throw new NotImplementedException();
            return map.MaybeGet(id).Select(e => mapping.FromEntity(e));
        }

        public async Task<string> InsertAsync(T instance, ISessionHandle session = null) => (await InsertAsync(new[] { instance }, session)).First();
        public async Task<Maybe<string>> UpsertAsync(T instance, ISessionHandle session = null) => (await UpsertAsync(new[] { instance }, session)).MaybeFirst();
        public async Task UpdateAsync(T instance, ISessionHandle session = null) => await UpdateAsync(new []{ instance }, session);
        public async Task DeleteAsync(T instance, ISessionHandle session = null) => await DeleteAsync(new []{ instance }, session);
        public async Task DeleteAsync(string id, ISessionHandle session = null) => await DeleteAsync(new []{ id }, session);
        
        public Maybe<T> FindById(string id, ISessionHandle session = null) => FindByIdAsync(id, session).Result;
        public ResultPage<T> Query(IFilter<T> filter, ISessionHandle session = null) => QueryAsync(filter, session).Result;
        public ResultPage<T> Query<TP>(Expression<Func<T, TP>> expression, object value, ISessionHandle session = null) => QueryAsync(expression, value, session).Result;
        public ResultPage<T> QueryAll(int? limit = null, int? skip = null, ISessionHandle session = null) => QueryAllAsync(limit, skip, session).Result;
        public string Insert(T instance, ISessionHandle session = null) => InsertAsync(instance, session).Result;
        public string[] Insert(T[] instances, ISessionHandle session = null) => InsertAsync(instances, session).Result;
        public void Update(T[] instances, ISessionHandle session = null) => UpdateAsync(instances, session).Wait();
        public void Update(T instance, ISessionHandle session = null) => UpdateAsync(instance, session).Wait();
        public Maybe<string> Upsert(T instance, ISessionHandle session = null) => UpsertAsync(instance, session).Result;
        public string[] Upsert(T[] instances, ISessionHandle session = null) => UpsertAsync(instances, session).Result;
        public void Delete(T[] instances, ISessionHandle session = null) => DeleteAsync(instances, session).Wait();
        public void Delete(T instance, ISessionHandle session = null) => DeleteAsync(instance, session).Wait();
        public void Delete(string[] ids, ISessionHandle session = null) => DeleteAsync(ids, session).Wait();
        public void Delete(string id, ISessionHandle session = null) => DeleteAsync(id, session).Wait();
    }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
}
