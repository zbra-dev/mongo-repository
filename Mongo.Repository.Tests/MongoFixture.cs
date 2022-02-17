using DockerComposeFixture;
using MongoDB.Driver;
using System;
using System.IO;
using System.Linq;
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

        private const string temp = @"
version: '3.8'

services:
  mongo1:
    container_name: mongo1
    image: mongo:4.4
    environment:
    - MONGO_INITDB_ROOT_USERNAME=root
    - MONGO_INITDB_ROOT_PASSWORD=dummy
    networks:
      - mongors-network
    volumes:
      - ./data/mongo-1:/data/db
    ports:
      - 27021:27017
    links:
      - mongo2
      - mongo3
    restart: always
    entrypoint: [ '/usr/bin/mongod', '--replSet', 'rsmongo', '--bind_ip_all']

  mongo2:
    container_name: mongo2
    image: mongo:4.4
    volumes:
      - ~/mongors/data2:/data/db
    networks:
      - mongors-network
    ports:
      - 27022:27017
    restart: always
    entrypoint: [ '/usr/bin/mongod', '--replSet', 'rsmongo', '--bind_ip_all']
  mongo3:
    container_name: mongo3
    image: mongo:4.4
    volumes:
      - ~/mongors/data3:/data/db
    networks:
      - mongors-network
    ports:
      - 27023:27017
    restart: always
    entrypoint: [ '/usr/bin/mongod', '--replSet', 'rsmongo', '--bind_ip_all']
networks:
  mongors-network:
    driver: bridge";
        private const string databaseName = "test";
        private string dockerComposeFile;

        private readonly Lazy<IMongoClient> mongoClient;

        public IMongoClient Client => mongoClient.Value;
        //public string ConnectionString => "mongodb://root:dummy@localhost:27018/";
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

            foreach (var collection in collections)
            {
                db.DropCollection(collection);
            }
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