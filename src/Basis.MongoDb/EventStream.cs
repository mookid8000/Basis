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
            if (!_database.GetCollectionNames().Contains(_collectionName))
            {
                var options = new CollectionOptionsBuilder()
                    .SetCapped(true)
                    .SetMaxSize(1024 * 1024);

                try
                {
                    _database.CreateCollection(_collectionName, options);
                }
                catch { }
            }

            _lastSeqNo = _streamHandler.GetLastSequenceNumber();
            Console.WriteLine("Last seq no: {0}", _lastSeqNo);

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
                    .Find(Query<EventBatch>.GT(b => b.SeqNo, _lastSeqNo))
                    .SetFlags(QueryFlags.TailableCursor | QueryFlags.AwaitData)
                    .SetSortOrder(SortBy.Ascending("$natural"));

                var rawEnumerator = results.GetEnumerator();

                using (var enumerator = rawEnumerator)
                {
                    while (_keepRunning)
                    {
                        if (enumerator.MoveNext())
                        {
                            var current = enumerator.Current;

                            Console.WriteLine("Got event: {0}", current.SeqNo);
                            Dispatch(new[] {current});

                            var newLastSeqNo = current.SeqNo;
                            _streamHandler.Commit(newLastSeqNo);
                            _lastSeqNo = newLastSeqNo;
                        }
                        else
                        {
                            Thread.Sleep(100);
                        }
                        //else if (enumerator.IsDead)
                        //{
                        //    break;
                        //}
                        //else if (!enumerator.IsServerAwaitCapable)
                        //{
                        //    // avoid thrashing
                        //    Thread.Sleep(1000);
                        //}
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("An error occurred while attempting to receive: {0}", exception);

                Thread.Sleep(1000);
            }
        }

        void Dispatch(IEnumerable<EventBatch> results)
        {
            foreach (var batch in results.OrderBy(r => r.SeqNo))
            {
                foreach (var evt in batch.Events)
                {
                    _streamHandler.ProcessEvents(new[] { evt });
                }
            }
        }

        public void Dispose()
        {
            Console.Write("Stopping stream reader... ");
            _keepRunning = false;
            if (!_backgroundWorker.Join(TimeSpan.FromSeconds(5)))
            {
                _backgroundWorker.Abort();
            }
            Console.WriteLine("Stopped!");
        }
    }
}
