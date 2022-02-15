using DockerComposeFixture;
using MongoDB.Driver;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Mongo.Repository.Tests
{
    [CollectionDefinition("DatastoreCollection")]
    public class MongoCollection : ICollectionFixture<MongoFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    public class MongoFixture : DockerFixture, IDisposable
    {
        private const string databaseName = "test";
        private string dockerComposeFile;

        public MongoFixture(IMessageSink output)
            : base(output)
        {
            InitOnce(() =>
            {
                dockerComposeFile = Path.GetTempFileName();

                var dockerComposeFileStream = GetType().Assembly
                    .GetManifestResourceStream("Mongo.Repository.Tests.mongo-docker-compose.yaml")
                    ?? throw new Exception("Embedded resource for docker compose file is missing");

                using var fileStream = File.Create(dockerComposeFile);

                dockerComposeFileStream.CopyTo(fileStream);

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

            foreach(var collection in collections)
            {
                db.DropCollection(collection);
            }
        }

        public IMongoDatabase GetDb()
        {
            var client = new MongoClient("mongodb://root:dummy@localhost:27018/");
            return client.GetDatabase(databaseName);
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