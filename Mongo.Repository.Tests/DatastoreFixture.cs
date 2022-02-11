using DockerComposeFixture;
using Google.Cloud.Mongo.V1;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Mongo.Repository.Tests
{
    [CollectionDefinition("DatastoreCollection")]
    public class DatastoreCollection : ICollectionFixture<DatastoreFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    public class DatastoreFixture : DockerFixture, IDisposable
    {
        private const string DockerCompose = @"
version: '2'

services:
    datastore:
        image: singularities/datastore-emulator
        environment:
        - DATASTORE_PROJECT_ID=project-test
        - DATASTORE_LISTEN_ADDRESS=0.0.0.0:8081
        ports:
        - '8081:8081'
        command: --consistency=1
        ";

        private string dockerComposeFile;

        public DatastoreFixture(IMessageSink output)
            : base(output)
        {
            InitOnce(() =>
            {
                dockerComposeFile = Path.GetTempFileName();
                File.WriteAllText(dockerComposeFile, DockerCompose);

                return new DockerFixtureOptions
                {
                    DockerComposeFiles = new string[] { dockerComposeFile },
                    CustomUpTest = lines => lines.Any(l => l.Contains("Dev App Server is now running"))
                };
            });
        }

        public DatastoreDb GetDb(string projectId = "project-test", string namespaceId = "test")
        {
            Environment.SetEnvironmentVariable("DATASTORE_EMULATOR_HOST", "127.0.0.1:8081");
            var dbBuilder = new DatastoreDbBuilder
            {
                ProjectId = projectId,
                NamespaceId = namespaceId,
                EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly,
            };
            return dbBuilder.Build();
        }

        public void ClearData(string projectId = "project-test", string namespaceId = "test")
        {
            var db = GetDb(projectId, namespaceId);
            var query = new Query();
            var result = db.RunQueryLazily(query)
                .Where(e => !e.Key.Path.Any(p => p.Kind.StartsWith("__"))); // some special objects exist in Datastore that can't be deleted
            foreach (var chunk in ChunkBy(result, 500))
            {
                db.Delete(chunk);
            }
        }

        // careful when using this method because it's not very memory efficient
        // so it should not be used to lists much larger then 1000 objects 
        public static List<List<T>> ChunkBy<T>(IEnumerable<T> source, int? chunkSize)
        {
            // https://stackoverflow.com/a/24087164/275559
            return source
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => chunkSize == null ? 0 : (x.Index / chunkSize.Value))
                .Select(x => x.Select(v => v.Value).ToList())
                .ToList();
        }

        public override void Dispose()
        {
            base.Dispose();
            File.Delete(dockerComposeFile);
        }
    }
}