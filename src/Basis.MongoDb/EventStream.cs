﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Basis.MongoDb
{
    public class EventStream : IDisposable
    {
        readonly List<Action<IEnumerable<object>>> _batchHandlers = new List<Action<IEnumerable<object>>>();
        readonly MongoDatabase _database;
        readonly string _collectionName;
        readonly Thread _backgroundWorker;

        volatile bool _keepRunning = true;
        long _sequencer;
        bool _started;

        public EventStream(MongoDatabase database, string collectionName)
        {
            _database = database;
            _collectionName = collectionName;
            _backgroundWorker = new Thread(PumpEvents);
        }

        public void Save(IEnumerable<object> events)
        {
            if (!_started)
            {
                throw new InvalidOperationException("Cannot save to event stream before it has been started!");
            }

            _database.GetCollection<EventBatch>(_collectionName)
                .Save(new EventBatch
                {
                    Events = events.ToArray(),
                    SeqNo = Interlocked.Increment(ref _sequencer)
                });
        }

        public void Handle(Action<IEnumerable<object>> batchHandler)
        {
            if (_started)
            {
                throw new InvalidOperationException("Cannot add handlers to event stream after it has been started!");
            }

            _batchHandlers.Add(batchHandler);
        }

        public void Start()
        {
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

        long lastSeqNo = 0;
        void PerformEventPumpCycle()
        {
            try
            {
                var results = _database.GetCollection<EventBatch>(_collectionName)
                    .Find(Query<EventBatch>.GT(b => b.SeqNo, lastSeqNo));

                Dispatch(results);

                lastSeqNo = results.Max(r => r.SeqNo);
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
                    foreach (var handler in _batchHandlers)
                    {
                        handler(new[] {evt});
                    }
                }
            }
        }

        class EventBatch
        {
            public ObjectId Id { get; set; }
            public object[] Events { get; set; }
            public long SeqNo { get; set; }
        }

        public void Dispose()
        {
            _keepRunning = false;
            _backgroundWorker.Join();
        }
    }
}
