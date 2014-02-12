namespace Basis.Messages
{
    public class RequestPlaybackArgs
    {
        public RequestPlaybackArgs(long currentSeqNo)
        {
            CurrentSeqNo = currentSeqNo;
        }

        public long CurrentSeqNo { get; protected set; }     
    }
}