using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

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

            var seqNoForThisEvent = Interlocked.Increment(ref _sequencer);

            Console.Write("Inserting {0}... ", seqNoForThisEvent);
            _database.GetCollection<EventBatch>(_collectionName)
                .Insert(new EventBatch
                {
                    Events = events.ToArray(),
                    SeqNo = seqNoForThisEvent
                });
            Console.WriteLine("Inserted!");
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

            _started = true;
        }

        public void Dispose()
        {
        }
    }
}
