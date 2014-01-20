using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MongoDB.Driver;

namespace Basis.MongoDb
{
    public class EventStore : IDisposable
    {
        readonly MongoDatabase _database;
        readonly string _collectionName;

        long _sequencer;
        bool _started;

        public EventStore(MongoDatabase database, string collectionName)
        {
            _database = database;
            _collectionName = collectionName;
        }

        public void Save(IEnumerable<object> events)
        {
            if (!_started)
            {
                throw new InvalidOperationException("Cannot save to event store before it has been started!");
            }

            _database.GetCollection<EventBatch>(_collectionName)
                .Save(new EventBatch
                {
                    Events = events.ToArray(),
                    SeqNo = Interlocked.Increment(ref _sequencer)
                });
        }

        public void Start()
        {
            _started = true;
        }

        public void Dispose()
        {
        }
    }
}
