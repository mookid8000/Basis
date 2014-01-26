using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin.Cors;
using Microsoft.Owin.Hosting;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Owin;

namespace Basis.MongoDb
{
    public class EventStore : IDisposable
    {
        readonly MongoDatabase _database;
        readonly string _collectionName;
        readonly string _listenUri;
        readonly Sequencer _sequencer;

        IDisposable _host;

        bool _started;

        public EventStore(MongoDatabase database, string collectionName, string listenUri)
        {
            _database = database;
            _collectionName = collectionName;
            _listenUri = listenUri;
            _sequencer = new Sequencer(0);
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

            _host = WebApp.Start(_listenUri, a =>
            {
                GlobalHost.DependencyResolver.Register(typeof(EventStoreHub), () => new EventStoreHub(_database, _sequencer, _collectionName));

                a.UseCors(CorsOptions.AllowAll);
                a.MapSignalR();
            });

            _started = true;
        }

        public void Dispose()
        {
            if (_host != null)
            {
                _host.Dispose();
                _host = null;
            }
        }
    }

    public class Sequencer
    {
        long _sequenceNumber;
        public Sequencer(long initializationValue)
        {
            _sequenceNumber = initializationValue;
        }

        public long GetNextSequenceNumber()
        {
            return Interlocked.Increment(ref _sequenceNumber);
        }
    }

    public class EventStoreHub : Hub
    {
        readonly MongoDatabase _database;
        readonly Sequencer _sequencer;
        readonly string _collectionName;

        public EventStoreHub(MongoDatabase database, Sequencer sequencer, string collectionName)
        {
            _database = database;
            _sequencer = sequencer;
            _collectionName = collectionName;
        }

        public async Task Save(EventBatchDto eventBatchToSave)
        {
            var seqNoForThisEvent = _sequencer.GetNextSequenceNumber();

            Console.Write("Inserting {0}... ", seqNoForThisEvent);
            _database.GetCollection<EventBatch>(_collectionName)
                .Insert(new EventBatch
                {
                    Events = eventBatchToSave.Events,
                    SeqNo = seqNoForThisEvent
                });
            Console.WriteLine("Inserted!");

            await Clients.All.Publish(eventBatchToSave);
        }
    }
}
