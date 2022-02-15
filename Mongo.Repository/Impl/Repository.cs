using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ZBRA.Commons;

namespace Mongo.Repository.Impl
{
    public class Repository<T> : IRepository<T>
    {
        private readonly IMongoCollection<BsonDocument> collection;
        private readonly IEntityMapping<T> mapping;
        private readonly IIdGenerator idGenerator;

        public Repository(IMongoDatabase db, Mappings mappings)
        {
            mapping = mappings.Get<T>();
            collection = db.GetCollection<BsonDocument>(mapping.Name);
            idGenerator = new ObjectIdGenerator();

            mapping.UniqueProperty.Consume(p =>
            {
                var member = p.ExtractPropertyInfo();
                var fieldName = mapping.GetFieldName(member)
                    .OrThrow(() => new ArgumentException($"Property {member.Name} not found"));

                var field = new StringFieldDefinition<BsonDocument>(fieldName);
                var options = new CreateIndexOptions() { Unique = true };
                var indexKeyDefinition = new IndexKeysDefinitionBuilder<BsonDocument>().Ascending(field);
                var indexModel = new CreateIndexModel<BsonDocument>(indexKeyDefinition, options);
                collection.Indexes.CreateOne(indexModel);
            });
        }

        private ObjectId CreateId(BsonDocument document)
        {
            var id = idGenerator.GenerateId(collection, document);
            return (ObjectId)id;
        }

        public async Task<ResultPage<T>> QueryAllAsync(int? limit = null, int? skip = null)
        {
            var query = collection
                .Find(new BsonDocument())
                .Limit(limit)
                .Skip(skip);
            var resultTask = query.ToListAsync();
            var countTask = query.CountDocumentsAsync();
            await Task.WhenAll(resultTask, countTask);

            return new ResultPage<T>(
                resultTask.Result.Select(i => mapping.FromEntity(i)),
                countTask.Result > skip + limit
            );
        }

        public async Task<ResultPage<T>> QueryAsync(IFilter<T> filter)
        {
            var resolver = new FilterResolver(mapping);
            var filterDefinition = filter.CreateFilter(resolver);
            var sortDefinition = filter.CreateSort(resolver);
            var query = collection
                .Find(filterDefinition)
                .Sort(sortDefinition)
                .Skip(filter.Skip)
                .Limit(filter.Take);
            var resultTask = query.ToListAsync();
            var countTask = query.CountDocumentsAsync();
            await Task.WhenAll(resultTask, countTask);

            return new ResultPage<T>(
                resultTask.Result.Select(i => mapping.FromEntity(i)),
                countTask.Result > filter.Skip + filter.Take
            );
        }

        public async Task<ResultPage<T>> QueryAsync<P>(Expression<Func<T, P>> expression, object value)
        {
            var member = expression.ExtractPropertyInfo();
            var fieldName = mapping.GetFieldName(member)
                .OrThrow(() => new ArgumentException($"Property {member.Name} not found"));

            var result = await collection
                .Find(new BsonDocument(fieldName, mapping.ConvertToValue(fieldName, value)))
                .ToListAsync();
            return new ResultPage<T>(
                result.Select(i => mapping.FromEntity(i)),
                false
            );
        }

        public async Task<Maybe<T>> FindByIdAsync(string id)
        {
            var result = await collection.Find(new BsonDocument("_id", new ObjectId(id))).ToListAsync();
            return result.Select(i => mapping.FromEntity(i)).MaybeSingle();
        }

        public async Task<string[]> InsertAsync(params T[] instances)
        {
            if (instances.Length == 0)
                return new string[] { };

            if (instances.Any(i => !KeyMapping.IsKeyEmpty(mapping.GetKeyValue(i))))
                throw new PersistenceException("Cannot insert instance that already has key set");

            var entities = instances.Select(i => mapping.ToEntity(i, CreateId)).ToArray();
            await collection.InsertManyAsync(entities);
            return entities.Select(i => i["_id"].ToString()).ToArray();
        }

        public async Task UpdateAsync(params T[] instances)
        {
            // TODO: OPTIMIZE (BULKWRITE?)
            foreach (var instance in instances)
            {
                var entity = mapping.ToEntity(instance);
                await collection.ReplaceOneAsync(new BsonDocument("_id", new ObjectId(mapping.GetKeyValue(instance))), entity);
            }
        }

        public async Task<Maybe<string>[]> UpsertAsync(params T[] instances)
        {
            // TODO: OPTIMIZE (BULKWRITE?)
            Maybe<string>[] results = new Maybe<string>[0];
            foreach (var instance in instances)
            {
                var entity = mapping.ToEntity(instance, CreateId);
                var result = await collection.ReplaceOneAsync(
                    new BsonDocument("_id", entity["_id"].AsObjectId),
                    entity,
                    new ReplaceOptions { IsUpsert = true });
                results.Append(result.UpsertedId.ToMaybe().Select(i => i.ToString()));
            }

            return results;
        }

        public async Task DeleteAsync(params T[] instances)
        {
            if (instances.Length == 0)
                return;
            var ids = instances.Select(i => new ObjectId(mapping.GetKeyValue(i))).ToArray();
            var filter = Builders<BsonDocument>.Filter.In("_id", ids);
            await collection.DeleteManyAsync(filter);
        }

        public async Task DeleteAsync(params string[] ids)
        {
            if (ids.Length == 0)
                return;

            var filter = Builders<BsonDocument>.Filter.In("_id", ids);
            await collection.DeleteManyAsync(filter);
        }

        public Task<ResultPage<T>> QueryAllAsync() => QueryAllAsync(null, null);
        public async Task<string> InsertAsync(T instance) => (await InsertAsync(new[] { instance })).First();
        public async Task<Maybe<string>> UpsertAsync(T instance) => (await UpsertAsync(new[] { instance })).First();

        public ResultPage<T> QueryAll() => QueryAllAsync().Result;
        public ResultPage<T> Query<P>(Expression<Func<T, P>> expression, object value) => QueryAsync(expression, value).Result;
        public ResultPage<T> Query(IFilter<T> filter) => QueryAsync(filter).Result;
        public ResultPage<T> QueryAll(int? limit = null, int? skip = null) => QueryAllAsync(limit, skip).Result;
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