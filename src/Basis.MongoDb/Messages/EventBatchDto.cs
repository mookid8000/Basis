namespace Basis.MongoDb.Messages
{
    public class EventBatchDto
    {
        public long SeqNo { get; set; }
        public byte[] Events { get; set; }
    }
}