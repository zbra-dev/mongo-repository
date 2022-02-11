using System;
using System.Collections.Generic;
using System.Linq;

namespace Mongo.Repository
{
    public class DatastoreConfig
    {
        public string ProjectId { get; set; }
        public string CredentialsPath { get; set; }
        public Dictionary<string, object> JsonCredentials { get; set; }
        public string NamespaceId { get; set; }

        public void Validate(bool emulatorEnabled)
        {
            if (new[] { ProjectId, NamespaceId }.Any(c => string.IsNullOrWhiteSpace(c)))
                throw new Exception("Invalid Datastore configuration - ProjectId and NamespaceId must have values");
            if (!emulatorEnabled && string.IsNullOrWhiteSpace(CredentialsPath) && JsonCredentials == null)
                throw new Exception("Invalid Datastore configuration - Missing credentials");
        }
    }
}
