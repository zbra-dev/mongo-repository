using Microsoft.Extensions.Configuration;
using Mongo.Repository;
using Mongo.Repository.Impl;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDatastoreRepository(this IServiceCollection services, IConfiguration configuration)
        {

            var datastoreConfig = configuration.GetSection("Datastore").Get<DatastoreConfig>();
            datastoreConfig.NamespaceId = configuration.GetValue<string>("DatastoreNamespace", datastoreConfig.NamespaceId);
            services.AddSingleton(datastoreConfig);

            var emulatorHost = configuration.GetValue<string>("DATASTORE_EMULATOR_HOST", null);
            services.AddSingleton(s =>
            {
                var config = s.GetService<DatastoreConfig>();
                config.Validate(emulatorHost != null);

                var dbBuilder = new DatastoreDbBuilder
                {
                    ProjectId = config.ProjectId,
                    NamespaceId = config.NamespaceId,
                };
                if (emulatorHost == null)
                {
                    dbBuilder.CredentialsPath = string.IsNullOrWhiteSpace(config.CredentialsPath) ? null : config.CredentialsPath;
                    dbBuilder.JsonCredentials = config.JsonCredentials == null ? null : JsonConvert.SerializeObject(config.JsonCredentials);
                }
                else
                {
                    dbBuilder.EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly;
                }
                return dbBuilder.Build();
            });

            services.AddSingleton(typeof(IRepository<>), typeof(Repository<>));

            return services;
        }
    }
}
