using System.Collections.Generic;
using System.Linq;

namespace Basis.Messages
{
    public class PlaybackEventBatch
    {
        public PlaybackEventBatch(IEnumerable<PlaybackEvent> events)
        {
            Events = events.ToList();
        }
        public List<PlaybackEvent> Events { get; protected set; }
    }


    public class PlaybackEvent
    {
        public PlaybackEvent(long seqNo, byte[] body)
        {
            SeqNo = seqNo;
            Body = body;
        }

        public long SeqNo { get; protected set; }
        public byte[] Body { get; protected set; }
    }
}