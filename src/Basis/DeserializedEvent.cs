namespace Basis
{
    public class DeserializedEvent
    {
        public DeserializedEvent(long seqNo, object evt)
        {
            SeqNo = seqNo;
            Event = evt;
        }

        public long SeqNo { get; private set; }
        public object Event { get; private set; }
    }
}