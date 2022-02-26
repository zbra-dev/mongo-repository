using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DockerComposeFixture;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace ZBRA.Mongo.Repository.Tests
{
    [CollectionDefinition("MongoCollection")]
    public class MongoCollection : ICollectionFixture<MongoFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    public class MongoFixture : DockerFixture, IDisposable
    {
        private const string DatabaseName = "test";

        private readonly Lazy<IMongoClient> mongoClient;

        public IMongoClient Client => mongoClient.Value;
        public string ConnectionString => "mongodb://localhost:27021/";

        public MongoFixture(IMessageSink output)
            : base(output)
        {
            mongoClient = new Lazy<IMongoClient>(GetClient);

            InitOnce(() =>
            {
                var assemblyPath = Path.GetDirectoryName(GetType().Assembly.Location);
                var dockerComposeFile = Path.Join(assemblyPath, "Mongo", "docker-compose.yaml");

                return new DockerFixtureOptions
                {
                    DockerComposeFiles = new string[] { dockerComposeFile },
                    CustomUpTest = lines => lines.Any(l => l.Contains("Replicaset is ready"))
                };
            });
        }

        public void ClearData()
        {
            var db = GetDb();
            var collections = db.ListCollectionNames().ToList();

            var tasks = collections.Select(c => db.DropCollectionAsync(c));
            Task.WaitAll(tasks.ToArray());
        }

        public IMongoDatabase GetDb()
        {
            return Client.GetDatabase(DatabaseName);
        }

        private IMongoClient GetClient()
        {
            return new MongoClient(ConnectionString);
        }
    }
}