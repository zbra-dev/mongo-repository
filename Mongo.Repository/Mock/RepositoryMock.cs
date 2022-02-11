using Google.Cloud.Mongo.V1;
using Mongo.Repository.Impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ZBRA.Commons;

namespace Mongo.Repository.Mock
{
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

    /// <summary>Class <c>RepositoryMock<></c> is meant to be used in very simple test cases.
    /// Overall its usage is discouraged and it will be removed in future versions.
    /// Instead tests should be done using a DatastoreFixture.
    /// </summary>
    public class RepositoryMock<T> : IRepository<T>
    {
        private static string[] KeyPropertyNames = new[] { "id", "key" };

        public bool IsRunningEmulator { get => false; }

        private readonly Dictionary<string, Entity> map = new Dictionary<string, Entity>();
        private readonly IEntityMapping<T> mapping;
        private readonly KeyFactory keyFactory;
        private readonly Random random = new Random();
        private Dictionary<Type, Func<IFilter<T>, ResultPage<T>, ResultPage<T>>> filterMap = new Dictionary<Type, Func<IFilter<T>, ResultPage<T>, ResultPage<T>>>();

        public RepositoryMock(Mappings mappings)
        {
            mapping = mappings.Get<T>();
            if (mapping.HasUniqueConstraint())
                throw new ArgumentException("RepositoryMock does not support mappings with unique constraint");
            keyFactory = new KeyFactory(new PartitionId("fake-project-id"), mapping.Name);
        }

        public RepositoryMock(Mappings mappings, T[] instances)
            : this(mappings)
        {
            Insert(instances);
        }

        public void Clear() => map.Clear();
        public void AddFilterFunc<F>(Func<IFilter<T>, ResultPage<T>, ResultPage<T>> filterFunc) where F : IFilter<T> => filterMap[typeof(F)] = filterFunc;

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
                    var entity = mapping.ToEntity(i, keyFactory);
                    var key = GenerateKey();
                    entity.Key = keyFactory.CreateKey(key);
                    map.Add(key.ToString(), entity);
                    return key.ToString();
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
            var member = expression.ExtractPropertyInfo();

            // start - adding this code just to help unit tests
            var fieldName = mapping.GetFieldName(member).OrThrow(() => new ArgumentException($"Property {member.Name} not found"));
            var query = new Query(mapping.Name) { Filter = Filter.Equal(fieldName, mapping.ConvertToValue(fieldName, value)) };
            // end - adding this code just to help unit tests

            var result = await QueryAllAsync();
            return new ResultPage<T>(result.Entities.Where(v => Equals(member.GetValue(v), value)), result.HasMoreResults);
        }

        public async Task UpdateAsync(params T[] instances)
        {
            foreach (var instance in instances)
            {
                var key = mapping.GetKeyValue(instance);
                map[key] = mapping.ToEntity(instance, keyFactory);
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

        public async Task<ResultPage<T>> QueryAsync(IFilter<T> filter, string startCursor = null)
        {
            // start - adding this code just to help unit tests
            var query = new Query(mapping.Name);
            filter.ApplyTo(query, new Repository<T>.FilterResolver(mapping));
            // end - adding this code just to help unit tests

            if (!filterMap.TryGetValue(filter.GetType(), out var filterFunc))
                throw new ArgumentException($"Filter [{filter.GetType()}] hasn't been mapped");
            var result = await QueryAllAsync();
            return filterFunc(filter, result);
        }

        public async Task<Maybe<T>> FindByIdAsync(string id)
        {
            // start - adding this code just to help unit tests
            var query = new Query(mapping.Name) { Filter = Filter.Equal("__key__", keyFactory.CreateKey(long.Parse(id))) };
            // end - adding this code just to help unit tests

            return map.MaybeGet(id).Select(e => mapping.FromEntity(e));
        }

        public async Task<ResultPage<T>> QueryAllAsync() => new ResultPage<T>(map.Values.Select(i => mapping.FromEntity(i)));
        public Task<ResultPage<T>> QueryAllAsync(int? limit = null, string startCursor = null) => throw new NotImplementedException();
        public async Task<string> InsertAsync(T instance) => (await InsertAsync(new[] { instance })).First();
        public async Task<Maybe<string>> UpsertAsync(T instance) => (await UpsertAsync(new[] { instance })).First();

        public Maybe<T> FindById(string id) => FindByIdAsync(id).Result;
        public ResultPage<T> Query(IFilter<T> filter, string startCursor = null) => QueryAsync(filter, startCursor).Result;
        public ResultPage<T> Query<P>(Expression<Func<T, P>> expression, object value) => QueryAsync(expression, value).Result;
        public ResultPage<T> QueryAll() => QueryAllAsync().Result;
        public ResultPage<T> QueryAll(int? limit = null, string startCursor = null) => QueryAllAsync(limit, startCursor).Result;
        public string Insert(T instance) => InsertAsync(instance).Result;
        public string[] Insert(params T[] instances) => InsertAsync(instances).Result;
        public void Update(params T[] instances) => UpdateAsync(instances).Wait();
        public Maybe<string> Upsert(T instance) => UpsertAsync(instance).Result;
        public Maybe<string>[] Upsert(params T[] instances) => UpsertAsync(instances).Result;
        public void Delete(params T[] instances) => DeleteAsync(instances).Wait();
        public void Delete(params string[] ids) => DeleteAsync(ids).Wait();

        protected IReadOnlyCollection<Entity> GetEntities() => map.Values;
    }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
}
