using MongoDB.Bson;

namespace Basis.MongoDb
{
    class EventBatch
    {
        public ObjectId Id { get; set; }
        public object[] Events { get; set; }
        public long SeqNo { get; set; }
    }
}