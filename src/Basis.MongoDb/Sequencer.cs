using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NLog;

namespace Basis.MongoDb
{
    public class Sequencer
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        readonly IStreamHandler _streamHandler;
        readonly SortedSet<DeserializedEvent> _events = new SortedSet<DeserializedEvent>();

        public Sequencer(IStreamHandler streamHandler)
        {
            _streamHandler = streamHandler;
        }

        public async Task Enqueue(DeserializedEvent deserializedEvent)
        {
            var lastSequenceNumber = _streamHandler.GetLastSequenceNumber();
            var expectedNextSequenceNumber = lastSequenceNumber + 1;

            if (expectedNextSequenceNumber == deserializedEvent.SeqNo)
            {
                await _streamHandler.ProcessEvents(deserializedEvent);
            }
            else
            {
                _events.Add(new DeserializedEvent(deserializedEvent.SeqNo, deserializedEvent));
            }

            if (_events.Count > 0)
            {
                Log.Info("Whoa, there's {0} events in the sorted set", _events.Count);

                while (_events.Min.SeqNo == expectedNextSequenceNumber)
                {
                    var stuff = _events.Min;
                    await _streamHandler.ProcessEvents(stuff);
                    _events.Remove(stuff);

                    expectedNextSequenceNumber++;
                }
            }
        }
    }

    public class DeserializedEvent : IComparable<DeserializedEvent>
    {
        public DeserializedEvent(long seqNo, object evt)
        {
            SeqNo = seqNo;
            Event = evt;
        }

        public long SeqNo { get; private set; }
        public object Event { get; private set; }
        public int CompareTo(DeserializedEvent other)
        {
            return SeqNo.CompareTo(other.SeqNo);
        }
    }
}