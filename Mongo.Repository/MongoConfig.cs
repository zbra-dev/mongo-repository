using System;
using System.Linq;

namespace Mongo.Repository
{
    public class MongoConfig
    {
        public string DatabaseName { get; set; }
        public string ConnString { get; set; }

        public void Validate()
        {
            if (new[] { ConnString, DatabaseName }.Any(c => string.IsNullOrWhiteSpace(c)))
                throw new Exception("Invalid Mongo configuration - ConnString and DatabaseName must have values");
        }
    }
}
