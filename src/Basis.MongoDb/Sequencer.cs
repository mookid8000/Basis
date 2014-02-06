using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace Basis.MongoDb
{
    public class Sequencer : IDisposable
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        readonly ConcurrentSortedSet<DeserializedEvent> _events = new ConcurrentSortedSet<DeserializedEvent>(new DeserializedEventComparer());
        readonly IStreamHandler _streamHandler;
        readonly Thread _dispatcher;

        volatile bool _keepRunning = true;

        readonly Semaphore _dispatcherThreadWorkSignal = new Semaphore(0, int.MaxValue);

        public Sequencer(IStreamHandler streamHandler)
        {
            _streamHandler = streamHandler;
            _dispatcher = new Thread(RunDispatcher);
            _dispatcher.Start();
        }

        public async Task Enqueue(DeserializedEvent deserializedEvent)
        {
            _events.Add(deserializedEvent);

            _dispatcherThreadWorkSignal.Release();
        }

        void RunDispatcher()
        {
            while (true)
            {
                _dispatcherThreadWorkSignal.WaitOne();

                if (!_keepRunning) break;

                try
                {
                    var lastSequenceNumber = _streamHandler.GetLastSequenceNumber();
                    var expectedNextSequenceNumber = lastSequenceNumber + 1;

                    DeserializedEvent deserializedEvent;

                    while ((deserializedEvent = _events.FirstOrDefault()) != null
                        && deserializedEvent.SeqNo == expectedNextSequenceNumber)
                    {
                        try
                        {
                            _streamHandler.ProcessEvents(deserializedEvent).Wait();
                        }
                        catch (Exception exception)
                        {
                            throw new ApplicationException(
                                string.Format("An error ocurred while processing event {0}: {1}",
                                    deserializedEvent.SeqNo, exception));
                        }

                        lastSequenceNumber = _streamHandler.GetLastSequenceNumber();
                        expectedNextSequenceNumber = lastSequenceNumber + 1;

                        // success? remove the event
                        if (expectedNextSequenceNumber > deserializedEvent.SeqNo)
                        {
                            _events.Remove(deserializedEvent);
                        }
                    }
                }
                catch (Exception exception)
                {
                    Log.Warn("An error occurred while processing events: {0}", exception);
                }
            }
        }

        public void Dispose()
        {
            try
            {
                _keepRunning = false;
                
                _dispatcherThreadWorkSignal.Release();
                _dispatcher.Join();
            }
            finally
            {
                _dispatcherThreadWorkSignal.Dispose();
            }
        }

        public long[] GetMissingSequenceNumbers()
        {
            
        }
    }

    class ConcurrentSortedSet<TItem>
    {
        readonly SortedSet<TItem> _sortedSet;
        public ConcurrentSortedSet(IComparer<TItem> comparer)
        {
            _sortedSet = new SortedSet<TItem>(comparer);
        }

        public void Add(TItem item)
        {
            lock (_sortedSet) _sortedSet.Add(item);
        }

        public TItem FirstOrDefault()
        {
            lock (_sortedSet) return _sortedSet.FirstOrDefault();
        }

        public void Remove(TItem item)
        {
            lock (_sortedSet) _sortedSet.Remove(item);
        }
    }

    internal class DeserializedEventComparer : IComparer<DeserializedEvent>
    {
        public int Compare(DeserializedEvent x, DeserializedEvent y)
        {
            return x.CompareTo(y);
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