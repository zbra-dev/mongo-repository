using System;
using System.Runtime.Serialization;

namespace ZBRA.Mongo.Repository.Impl
{
    [Serializable]
    public class EntityMappingNotFoundException : Exception
    {
        public Type UnknownType { get; }

        public EntityMappingNotFoundException(Type unknownType)
            : base($"Entity mapping for [{unknownType.Name}] not found")
        {
            UnknownType = unknownType;
        }

        public EntityMappingNotFoundException(string message) : base(message)
        {
        }

        public EntityMappingNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected EntityMappingNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}