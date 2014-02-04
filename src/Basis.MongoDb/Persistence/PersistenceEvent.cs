using System.Collections.Generic;

namespace Basis.MongoDb.Persistence
{
    class PersistenceEvent
    {
        public long SeqNo { get; set; }
        public Dictionary<string,string> Meta { get; set; }
        public byte[] Body { get; set; }
    }
}