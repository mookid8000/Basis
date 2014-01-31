﻿using MongoDB.Bson;

namespace Basis.MongoDb.Messages
{
    class EventBatch
    {
        public ObjectId Id { get; set; }
        public byte[] Events { get; set; }
        public long SeqNo { get; set; }
    }
}