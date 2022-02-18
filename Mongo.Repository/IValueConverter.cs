using MongoDB.Bson;
using System;
using System.Collections.Generic;

namespace ZBRA.Mongo.Repository
{
    internal interface IValueConverter
    {
        Type Type { get; }
        object FromValue(BsonValue value);
        BsonValue ToValue(object obj);
        IList<IEntityMigration> FindMigrations();
    }
}
