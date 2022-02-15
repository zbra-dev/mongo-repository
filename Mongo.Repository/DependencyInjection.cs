using Mongo.Repository;
using Mongo.Repository.Impl;
using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddMongoRepository(
            this IServiceCollection services,
            MongoConfig config)
        {
            services.AddSingleton<IMongoClient>(new MongoClient(config.ConnString));
            services.AddScoped(provider => provider.GetService<IMongoClient>().GetDatabase(config.DatabaseName));
            services.AddSingleton(typeof(IRepository<>), typeof(Repository<>));

            return services;
        }
    }
}
