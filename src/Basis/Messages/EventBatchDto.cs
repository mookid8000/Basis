using System.Collections.Generic;
using System.Linq;

namespace Basis.Messages
{
    /// <summary>
    /// Represents a batch of events that can be delivered to a stream client. The events may be ordered in
    /// any way and may contain gaps etc.
    /// </summary>
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