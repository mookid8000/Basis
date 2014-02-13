using System;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace Basis
{
    public class Sequencer : IDisposable
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        
        readonly ConcurrentSortedSet _events = new ConcurrentSortedSet(new DeserializedEventComparer())
        {
            MaxSize = 1000
        };
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

        public void RunDispatcherForSafetysSake()
        {
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
                    PossiblyProcessEvents();
                }
                catch (Exception exception)
                {
                    Log.Warn("An error occurred while processing events: {0}", exception);
                }
            }
        }

        void PossiblyProcessEvents()
        {
            var lastSequenceNumber = _streamHandler.GetLastSequenceNumber();
            var expectedNextSequenceNumber = lastSequenceNumber + 1;

            DeserializedEvent deserializedEvent;

            while ((deserializedEvent = _events.FirstOrDefault()) != null
                   && deserializedEvent.SeqNo <= expectedNextSequenceNumber)
            {
                if (deserializedEvent.SeqNo < expectedNextSequenceNumber)
                {
                    Log.Debug("Skipping {0}", deserializedEvent.SeqNo);
                    _events.Remove(deserializedEvent);
                    continue;
                }

                try
                {
                    Log.Debug("Processing {0}", deserializedEvent.SeqNo);
                    _streamHandler.ProcessEvent(deserializedEvent).Wait();
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
            var lastSequenceNumber = _streamHandler.GetLastSequenceNumber();
            
            return _events.GetGaps(lastSequenceNumber);
        }
    }
}