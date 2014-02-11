using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;

namespace Basis.Persistence
{
    class PersistenceEventBatch
    {
        public PersistenceEventBatch(IEnumerable<PersistenceEvent> events)
        {
            Id = ObjectId.GenerateNewId();
            Events = events.ToList();
        }
        public ObjectId Id { get; protected set; }
        public List<PersistenceEvent> Events { get; protected set; }

        public long FirstSeqNo
        {
            get { return Events.Min(e => e.SeqNo); }
            protected set { }
        }
    }
}