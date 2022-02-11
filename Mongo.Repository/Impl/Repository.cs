using Google.Api.Gax;
using Google.Cloud.Mongo.V1;
using Google.Protobuf;
using Grpc.Core;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ZBRA.Commons;

namespace Mongo.Repository.Impl
{
    public class Repository<T> : IRepository<T>
    {
        public bool IsRunningEmulator { get; }
        private readonly DatastoreDb db;
        private readonly IEntityMapping<T> mapping;
        private readonly KeyFactory keyFactory;
        private readonly KeyFactory uniqueKeyFactory;

        public Repository(DatastoreDb db, Mappings mappings)
        {
            this.db = db;
            this.mapping = mappings.Get<T>();
            this.keyFactory = !mapping.Name.EndsWith("unique") ? db.CreateKeyFactory(mapping.Name) : throw new ArgumentException("Entity name cannot end with 'unique'");
            this.uniqueKeyFactory = db.CreateKeyFactory($"{mapping.Name}_unique");
            IsRunningEmulator = Platform.Instance().Type == PlatformType.Unknown;
        }

        private async Task<ResultPage<T>> ToPage(DatastoreQueryResults result, Query query)
        {
            var hasMoreResults = result.MoreResults != QueryResultBatch.Types.MoreResultsType.NoMoreResults;

            // there's a known bug with Datastore emulator
            // reference: https://github.com/googleapis/google-cloud-node/issues/2846
            // we need to fix the behavior to avoid running queries forever
            if (IsRunningEmulator && hasMoreResults)
            {
                var hasMoreQuery = query.Clone();
                hasMoreQuery.Offset = query.Offset + result.Entities.Count;
                hasMoreQuery.Limit = 1;

                var hasMoreQueryResult = await db.RunQueryAsync(hasMoreQuery);

                hasMoreResults = hasMoreQueryResult.Entities.Count > 0;
            }

            return new ResultPage<T>(
                result.Entities.Select(i => mapping.FromEntity(i)),
                hasMoreResults,
                result.EndCursor.ToBase64()
            );
        }

        public async Task<ResultPage<T>> QueryAllAsync(int? limit = null, string startCursor = null)
        {
            var query = new Query(mapping.Name) { Limit = limit };
            if (startCursor != null)
                query.StartCursor = ByteString.FromBase64(startCursor);
            var result = await db.RunQueryAsync(query);
            return await ToPage(result, query);
        }

        public async Task<ResultPage<T>> QueryAsync(IFilter<T> filter, string startCursor = null)
        {
            var query = new Query(mapping.Name);
            filter.ApplyTo(query, new FilterResolver(mapping));
            if (startCursor != null)
                query.StartCursor = ByteString.FromBase64(startCursor);
            var result = await db.RunQueryAsync(query);
            return await ToPage(result, query);
        }

        public async Task<ResultPage<T>> QueryAsync<P>(Expression<Func<T, P>> expression, object value)
        {
            var member = expression.ExtractPropertyInfo();
            var fieldName = mapping.GetFieldName(member).OrThrow(() => new ArgumentException($"Property {member.Name} not found"));
            var query = new Query(mapping.Name) { Filter = Filter.Equal(fieldName, mapping.ConvertToValue(fieldName, value)) };
            var result = await db.RunQueryAsync(query);
            return await ToPage(result, query);
        }

        public async Task<Maybe<T>> FindByIdAsync(string id)
        {
            var entity = await FindEntityByIdAsync(id);
            return entity.Select(i => mapping.FromEntity(i));
        }

        private async Task<Maybe<Entity>> FindEntityByIdAsync(string id)
        {
            var query = new Query(mapping.Name) { Filter = Filter.Equal("__key__", keyFactory.CreateKey(long.Parse(id))) };
            var result = await db.RunQueryAsync(query);
            return result.Entities.MaybeSingle();
        }

        public async Task<string[]> InsertAsync(params T[] instances)
        {
            if (instances.Length == 0)
                return new string[] { };

            if (instances.Any(i => !KeyMapping.IsKeyEmpty(mapping.GetKeyValue(i))))
                throw new PersistenceException("Cannot insert instance that already has key set");

            var entities = instances.Select(i => mapping.ToEntity(i, keyFactory)).ToArray();
            var uniques = mapping.HasUniqueConstraint() ? instances.Select(i => mapping.ToUnique(i, uniqueKeyFactory)).ToArray() : new Entity[] { };
            using (var transaction = await db.BeginTransactionAsync())
            {
                transaction.Insert(uniques);
                transaction.Insert(entities);
                try
                {
                    var response = await transaction.CommitAsync();
                    return response.MutationResults
                        .Skip(uniques.Length)
                        .Select(r => r.Key.Path.First().Id.ToString())
                        .ToArray();
                }
                catch (RpcException ex)
                {
                    if (mapping.HasUniqueConstraint() && ex.StatusCode == StatusCode.AlreadyExists)
                    {
                        throw new UniqueConstraintException();
                    }
                    throw;
                }
            }
        }

        public async Task UpdateAsync(params T[] instances)
        {
            if (instances.Length == 0)
                return;

            if (mapping.HasUniqueConstraint())
            {
                // if using unique constraints we need to update both the entities and the unique values
                var entities = instances.Select(i =>
                {
                    var id = mapping.GetKeyValue(i);
                    if (id == null)
                        throw new PersistenceException("Cannot update an entity that has key null");
                    var existing = FindEntityByIdAsync(id).Result
                        .OrThrow(() => throw new PersistenceException($"Entity[{id}] not found"));

                    var entity = mapping.ToEntity(i, keyFactory);
                    var unique = mapping.ToUnique(i, uniqueKeyFactory);
                    return new { existing, entity, unique };
                }).ToArray();

                using (var transaction = await db.BeginTransactionAsync())
                {
                    foreach (var i in entities)
                    {
                        var newValue = (string)i.entity[Constants.UniqueValueFieldName];
                        var existingValue = (string)i.existing[Constants.UniqueValueFieldName];
                        if (newValue != existingValue)
                        {
                            if (existingValue != null) // if existing value is null this entity didn't have a key set yet
                                transaction.Delete(uniqueKeyFactory.CreateKey(existingValue));
                            transaction.Insert(i.unique);
                        }
                        transaction.Update(i.entity);
                    }

                    try
                    {
                        var _ = await transaction.CommitAsync();
                    }
                    catch (RpcException ex)
                    {
                        if (mapping.HasUniqueConstraint() && ex.StatusCode == StatusCode.AlreadyExists)
                        {
                            throw new UniqueConstraintException();
                        }
                        throw;
                    }
                }
            }
            else
            {
                if (instances.Any(i => KeyMapping.IsKeyEmpty(mapping.GetKeyValue(i))))
                    throw new PersistenceException("Cannot update an entity that has key null");

                var entities = instances.Select(i => mapping.ToEntity(i, keyFactory)).ToArray();
                using (var transaction = await db.BeginTransactionAsync())
                {
                    transaction.Update(entities);
                    var _ = await transaction.CommitAsync();
                }
            }
        }

        public async Task<Maybe<string>[]> UpsertAsync(params T[] instances)
        {
            if (mapping.HasUniqueConstraint())
                throw new InvalidOperationException("Upsert is not supported when mapping has a unique constraint. Split your transaction to use update/insert separately.");
            if (instances.Length == 0)
                return new Maybe<string>[] { };

            var entities = instances.Select(i => mapping.ToEntity(i, keyFactory)).ToArray();

            using (var transaction = await db.BeginTransactionAsync())
            {
                transaction.Upsert(entities);
                var response = await transaction.CommitAsync();
                return response.MutationResults
                    .Select(r => r.Key == null ? Maybe<string>.Nothing : r.Key.Path.First().Id.ToString().ToMaybe())
                    .ToArray();
            }
        }

        public async Task DeleteAsync(params T[] instances)
        {
            if (instances.Length == 0)
                return;

            var entities = instances.Select(i => mapping.ToEntity(i, keyFactory)).ToArray();
            var uniques = mapping.HasUniqueConstraint() ? instances.Select(i => mapping.ToUnique(i, uniqueKeyFactory)).ToArray() : new Entity[] { };
            using (var transaction = await db.BeginTransactionAsync())
            {
                transaction.Delete(uniques);
                transaction.Delete(entities);
                var _ = await transaction.CommitAsync();
            }
        }

        public async Task DeleteAsync(params string[] ids)
        {
            if (ids.Length == 0)
                return;

            if (mapping.HasUniqueConstraint())
                throw new InvalidOperationException("Cannot delete by ids when there's a unique constraint");

            var keys = ids.Select(i => keyFactory.CreateKey(long.Parse(i))).ToArray();
            using (var transaction = await db.BeginTransactionAsync())
            {
                transaction.Delete(keys);
                var _ = await transaction.CommitAsync();
            }
        }

        public Task<ResultPage<T>> QueryAllAsync() => QueryAllAsync(null, null);
        public async Task<string> InsertAsync(T instance) => (await InsertAsync(new[] { instance })).First();
        public async Task<Maybe<string>> UpsertAsync(T instance) => (await UpsertAsync(new[] { instance })).First();

        public ResultPage<T> QueryAll() => QueryAllAsync().Result;
        public ResultPage<T> Query<P>(Expression<Func<T, P>> expression, object value) => QueryAsync(expression, value).Result;
        public ResultPage<T> Query(IFilter<T> filter, string startCursor = null) => QueryAsync(filter, startCursor).Result;
        public ResultPage<T> QueryAll(int? limit = null, string startCursor = null) => QueryAllAsync(limit, startCursor).Result;
        public Maybe<T> FindById(string id) => FindByIdAsync(id).Result;
        public string Insert(T instance) => InsertAsync(instance).Result;
        public string[] Insert(params T[] instances) => InsertAsync(instances).Result;
        public void Update(params T[] instances) => UpdateAsync(instances).Wait();
        public Maybe<string> Upsert(T instance) => UpsertAsync(instance).Result;
        public Maybe<string>[] Upsert(params T[] instances) => UpsertAsync(instances).Result;
        public void Delete(params T[] instances) => DeleteAsync(instances).Wait();
        public void Delete(params string[] ids) => DeleteAsync(ids).Wait();

        internal class FilterResolver : IFieldResolver<T>
        {
            private IEntityMapping<T> mapping;

            public FilterResolver(IEntityMapping<T> mapping)
            {
                this.mapping = mapping;
            }

            public string FieldName<P>(Expression<Func<T, P>> expression)
            {
                var property = expression.ExtractPropertyInfo();
                return mapping.GetFieldName(property)
                    .OrThrow(() => new ArgumentException($"Property [{property.Name}] not found"));
            }

            public string FieldName(string propertyName)
            {
                return mapping.GetFieldName(propertyName)
                    .OrThrow(() => new ArgumentException($"Property [{propertyName}] not found"));
            }
        }
    }
}