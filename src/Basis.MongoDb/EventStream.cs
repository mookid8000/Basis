using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Basis.MongoDb
{
    public class EventStream : IDisposable
    {
        readonly MongoDatabase _database;
        readonly string _collectionName;
        readonly IStreamHandler _streamHandler;
        readonly Thread _backgroundWorker;

        volatile bool _keepRunning = true;
        bool _started;
        long _lastSeqNo = -1;

        public EventStream(MongoDatabase database, string collectionName, IStreamHandler streamHandler)
        {
            _database = database;
            _collectionName = collectionName;
            _streamHandler = streamHandler;
            _backgroundWorker = new Thread(PumpEvents);
        }

        public void Start()
        {
            _lastSeqNo = _streamHandler.GetLastSequenceNumber();

            _backgroundWorker.Start();
            _started = true;
        }

        void PumpEvents()
        {
            while (_keepRunning)
            {
                PerformEventPumpCycle();
            }
        }
        void PerformEventPumpCycle()
        {
            try
            {
                var results = _database.GetCollection<EventBatch>(_collectionName)
                    .Find(Query<EventBatch>.GT(b => b.SeqNo, _lastSeqNo));

                Dispatch(results);

                var newLastSeqNo = results.Max(r => r.SeqNo);
                _streamHandler.Commit(newLastSeqNo);
                _lastSeqNo = newLastSeqNo;
            }
            catch (Exception exception)
            {
                // oh noes?!?!?!
            }
        }

        void Dispatch(IEnumerable<EventBatch> results)
        {
            foreach (var batch in results.OrderBy(r => r.SeqNo))
            {
                foreach (var evt in batch.Events)
                {
                    _streamHandler.ProcessEvents(new[] {evt});
                }
            }
        }

        public void Dispose()
        {
            _keepRunning = false;
            _backgroundWorker.Join();
        }
    }
}
