using DockerComposeFixture;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ZBRA.Mongo.Repository.Tests;

[CollectionDefinition("MongoCollection")]
public class MongoCollection : ICollectionFixture<MongoFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

public class MongoFixture : DockerFixture, IDisposable
{
    private readonly Lazy<IMongoClient> mongoClient;

    public IMongoClient Client => mongoClient.Value;
    public string ConnectionString => "mongodb://127.0.0.1:27021/";
    public string DatabaseName => "test";

    public MongoFixture(IMessageSink output) : base(output)
    {
        mongoClient = new Lazy<IMongoClient>(GetClient);
    }

    public IMongoDatabase GetDb()
    {
        return Client.GetDatabase(DatabaseName);
    }

    private IMongoClient GetClient()
    {
        return new MongoClient(ConnectionString);
    }

    public void ClearData()
    {
        var db = GetDb();
        var collections = db.ListCollectionNames().ToList();

        var tasks = collections.Select(c => db.DropCollectionAsync(c));
        Task.WaitAll(tasks.ToArray());
    }
}