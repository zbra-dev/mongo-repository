using DockerComposeFixture;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Mongo.Repository.Tests
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
        private const string DockerCompose = @"
version: '2'

services:
    mongo:
        image: mongo
        environment:
        - MONGO_INITDB_ROOT_USERNAME=root
        - MONGO_INITDB_ROOT_PASSWORD=dummy
        ports:
        - '27018:27017'";
        private const string databaseName = "test";
        private string dockerComposeFile;

        private readonly Lazy<IMongoClient> mongoClient;

        public IMongoClient Client => mongoClient.Value;
        public string ConnectionString => "mongodb://root:dummy@localhost:27018/";

        public MongoFixture(IMessageSink output)
            : base(output)
        {
            mongoClient = new Lazy<IMongoClient>(GetClient);

            InitOnce(() =>
            {
                dockerComposeFile = Path.GetTempFileName();
                File.WriteAllText(dockerComposeFile, DockerCompose);

                return new DockerFixtureOptions
                {
                    DockerComposeFiles = new string[] { dockerComposeFile },
                    CustomUpTest = lines => lines.Any(l => l.Contains("Waiting for connections"))
                };
            });
        }

        public void ClearData()
        {
            var db = GetDb();
            var collections = db.ListCollectionNames().ToList();

            var tasks = new Task[0];
            foreach(var collection in collections)
            {
                var task = db.DropCollectionAsync(collection);
                tasks.Append(task);
            }

            Task.WaitAll(tasks);
        }

        public IMongoDatabase GetDb()
        {
            return Client.GetDatabase(databaseName);
        }

        private IMongoClient GetClient()
        {
            return new MongoClient(ConnectionString);
        }

        public override void Dispose()
        {
            base.Dispose();
            if (dockerComposeFile != null)
            {
                File.Delete(dockerComposeFile);
            }
        }
    }
}