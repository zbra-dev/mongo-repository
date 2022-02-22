using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ZBRA.Maybe;

namespace ZBRA.Mongo.Repository.Impl
{
    public class Repository<T> : IRepository<T>
    {
        private readonly IMongoClient client;
        private readonly IMongoCollection<BsonDocument> collection;
        private readonly IEntityMapping<T> mapping;
        private readonly IIdGenerator idGenerator;

        public Repository(IMongoClient client, IMongoDatabase db, Mappings mappings)
        {
            this.client = client;
            mapping = mappings.Get<T>();
            collection = db.GetCollection<BsonDocument>(mapping.Name);
            idGenerator = new ObjectIdGenerator();
            CreateUniqueIndex();
        }

        private void CreateUniqueIndex()
        {
            if (mapping.UniqueProperty == null)
                return;
            
            var fieldName = mapping.GetFieldName(mapping.UniqueProperty)
                .OrThrow(() => new ArgumentException($"Property {mapping.UniqueProperty.Name} not found"));

            var field = new StringFieldDefinition<BsonDocument>(fieldName);
            var options = new CreateIndexOptions() { Unique = true };
            var indexKeyDefinition = new IndexKeysDefinitionBuilder<BsonDocument>().Ascending(field);
            var indexModel = new CreateIndexModel<BsonDocument>(indexKeyDefinition, options);
            collection.Indexes.CreateOne(indexModel);
        }
        
        private static bool IsDuplicateKeyError(MongoBulkWriteException ex) =>
            ex != null && ex.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey);

        private ObjectId CreateId(BsonDocument document)
        {
            var id = idGenerator.GenerateId(collection, document);
            return (ObjectId)id;
        }

        private ResultPage<T> ToPage(IList<BsonDocument> result, int? limit)
        {
            var records = result
                .Take(limit ?? int.MaxValue)
                .Select(i => mapping.FromEntity(i))
                .ToArray();
            return new ResultPage<T>(records, limit.HasValue && result.Count > limit.Value);
        }
        
        private IFindFluent<BsonDocument, BsonDocument> CreateQuery(FilterDefinition<BsonDocument> filter, ISessionHandle session)
        {
            return session == null
                ? collection.Find(filter)
                : collection.Find(((SessionHandle) session).InnerSession, filter);   
        }

        public async Task<ResultPage<T>> QueryAllAsync(int? limit = null, int? skip = null, ISessionHandle session = null)
        {
            var result = await CreateQuery(new BsonDocument(), session)
                .Limit((limit + 1) * -1) // take one extra record and tells the server to close the cursor afterwards
                .Skip(skip)
                .ToListAsync();

            return ToPage(result, limit);
        }

        public async Task<ResultPage<T>> QueryAsync(IFilter<T> filter, ISessionHandle session = null)
        {
            var resolver = new FilterResolver(mapping);
            var filterDefinition = filter.CreateFilter(resolver);
            var sortDefinition = filter.CreateSort(resolver);
            var result = await CreateQuery(filterDefinition, session)
                .Sort(sortDefinition)
                .Skip(filter.Skip)
                .Limit((filter.Take + 1) * -1) // take one extra record and tells the server to close the cursor afterwards
                .ToListAsync();

            return ToPage(result, filter.Take);
        }

        public async Task<ResultPage<T>> QueryAsync<P>(Expression<Func<T, P>> expression, object value, ISessionHandle session = null)
        {
            var member = expression.ExtractPropertyInfo();
            var fieldName = mapping.GetFieldName(member)
                .OrThrow(() => new ArgumentException($"Property {member.Name} not found"));

            var filter = new BsonDocument(fieldName, mapping.ConvertToValue(fieldName, value));
            var result = await CreateQuery(filter, session).ToListAsync();

            return new ResultPage<T>(result.Select(i => mapping.FromEntity(i)).ToArray());
        }

        public async Task<Maybe<T>> FindByIdAsync(string id, ISessionHandle session = null)
        {
            var filter = new BsonDocument("_id", new ObjectId(id));
            var result = await CreateQuery(filter, session).ToListAsync();
            return result.Select(i => mapping.FromEntity(i)).MaybeSingle();
        }

        public async Task<string[]> InsertAsync(T[] instances, ISessionHandle session = null)
        {
            if (instances.Length == 0)
                return new string[] { };

            if (instances.Any(i => !KeyMapping.IsKeyEmpty(mapping.GetKeyValue(i))))
                throw new PersistenceException("Cannot insert instance that already has key set");

            var entities = instances.Select(i => mapping.ToEntity(i, CreateId)).ToArray();
            try
            {
                if (session == null)
                    await collection.InsertManyAsync(entities);
                else
                    await collection.InsertManyAsync(((SessionHandle)session).InnerSession, entities);
            }
            catch (Exception ex)
            {
                if (mapping.UniqueProperty != null && IsDuplicateKeyError(ex as MongoBulkWriteException))
                    throw new UniqueConstraintException();
                throw;
            }
            return entities.Select(i => i["_id"].ToString()).ToArray();
        }

        public async Task UpdateAsync(T[] instances, ISessionHandle session = null)
        {
            if (instances.Length == 0)
                return;

            var replaceOneModels = new List<ReplaceOneModel<BsonDocument>>();
            foreach (var instance in instances)
            {
                var id = mapping.GetKeyValue(instance);
                if (id == null)
                    throw new PersistenceException("Cannot update an entity that has key null");
                var entity = mapping.ToEntity(instance);
                var replaceOneModel = new ReplaceOneModel<BsonDocument>(new BsonDocument("_id", new ObjectId(id)), entity);
                replaceOneModels.Add(replaceOneModel);
            }
            
            try
            {
                var result = session == null ?
                    await collection.BulkWriteAsync(replaceOneModels) :    
                    await collection.BulkWriteAsync(((SessionHandle)session).InnerSession, replaceOneModels);
            }
            catch (Exception ex)
            {
                if (mapping.UniqueProperty != null && IsDuplicateKeyError(ex as MongoBulkWriteException))
                    throw new UniqueConstraintException();
                throw;
            }
        }

        public async Task<string[]> UpsertAsync(T[] instances, ISessionHandle session = null)
        {
            if (instances.Length == 0)
            {
                return Array.Empty<string>();
            }

            var replaceOneModels = instances
                .Select(instance => mapping.ToEntity(instance, CreateId))
                .Select(entity => new ReplaceOneModel<BsonDocument>(
                    new BsonDocument("_id", entity["_id"].AsObjectId), entity)
                    {
                        IsUpsert = true,
                    })
                .ToList();

            try
            {
                var result = session == null ?
                    await collection.BulkWriteAsync(replaceOneModels):
                    await collection.BulkWriteAsync(((SessionHandle)session).InnerSession, replaceOneModels);
                return result.Upserts.Select(u => u.Id.AsObjectId.ToString()).ToArray();
            }
            catch (Exception ex)
            {
                if (mapping.UniqueProperty != null && IsDuplicateKeyError(ex as MongoBulkWriteException))
                    throw new UniqueConstraintException();
                throw;
            }
        }

        public async Task DeleteAsync(params T[] instances)
        {
            if (instances.Length == 0)
                return;
            var ids = instances.Select(i => new ObjectId(mapping.GetKeyValue(i))).ToArray();
            var filter = Builders<BsonDocument>.Filter.In("_id", ids);
            
            using var session = await client.StartSessionAsync();
            session.StartTransaction();
            try
            {
                await collection.DeleteManyAsync(session, filter);
                await session.CommitTransactionAsync();
            }
            catch (Exception)
            {
                await session.AbortTransactionAsync();
                throw;
            }
        }

        public async Task DeleteAsync(params string[] ids)
        {
            if (ids.Length == 0)
                return;

            var filter = Builders<BsonDocument>.Filter.In("_id", ids.Select(id => new ObjectId(id)).ToArray());
            using var session = await client.StartSessionAsync();
            session.StartTransaction();
            try
            {
                await collection.DeleteManyAsync(session, filter);
                await session.CommitTransactionAsync();
            }
            catch (Exception)
            {
                await session.AbortTransactionAsync();
                throw;
            }
        }

        public async Task<ISessionHandle> StartSessionAsync()
        {
            var session = await client.StartSessionAsync();
            return new SessionHandle(session);
        }

        public Task<ResultPage<T>> QueryAllAsync() => QueryAllAsync(null, null, null);
        public async Task<string> InsertAsync(T instance, ISessionHandle session = null) => (await InsertAsync(new[] { instance }, session)).First();
        public async Task<Maybe<string>> UpsertAsync(T instance, ISessionHandle session = null) => (await UpsertAsync(new[] { instance }, session)).MaybeFirst();
        public async Task UpdateAsync(T instance, ISessionHandle session = null) => await UpdateAsync(new[] { instance }, session);

        public ResultPage<T> QueryAll() => QueryAllAsync().Result;
        public ResultPage<T> Query<P>(Expression<Func<T, P>> expression, object value, ISessionHandle session = null) => QueryAsync(expression, value, session).Result;
        public ResultPage<T> Query(IFilter<T> filter, ISessionHandle session = null) => QueryAsync(filter, session).Result;
        public ResultPage<T> QueryAll(int? limit = null, int? skip = null, ISessionHandle session = null) => QueryAllAsync(limit, skip, session).Result;
        public Maybe<T> FindById(string id, ISessionHandle session = null) => FindByIdAsync(id, session).Result;
        public string Insert(T instance, ISessionHandle session = null) => InsertAsync(instance, session).Result;
        public string[] Insert(T[] instances, ISessionHandle session = null) => InsertAsync(instances, session).Result;
        public void Update(T[] instances, ISessionHandle session = null) => UpdateAsync(instances, session).Wait();
        public void Update(T instance, ISessionHandle session = null) => UpdateAsync(instance, session).Wait();
        public Maybe<string> Upsert(T instance, ISessionHandle session = null) => UpsertAsync(instance, session).Result;
        public string[] Upsert(T[] instances, ISessionHandle session = null) => UpsertAsync(instances, session).Result;
        public void Delete(params T[] instances) => DeleteAsync(instances).Wait();
        public void Delete(params string[] ids) => DeleteAsync(ids).Wait();
        public ISessionHandle StartSession() => StartSessionAsync().Result;

        internal class FilterResolver : IFieldResolver<T>
        {
            private readonly IEntityMapping<T> mapping;

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