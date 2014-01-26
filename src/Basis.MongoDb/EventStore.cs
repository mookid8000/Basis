using System;
using System.Linq;
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
        readonly string _listenUri;
        readonly string _collectionName;
        readonly MongoDatabase _database;
        readonly SequenceNumberGenerator _sequenceNumberGenerator;

        IDisposable _host;

        bool _started;

        public EventStore(MongoDatabase database, string collectionName, string listenUri)
        {
            _database = database;
            _collectionName = collectionName;
            _listenUri = listenUri;
            _sequenceNumberGenerator = new SequenceNumberGenerator(0);
        }

        public void Start()
        {
            if (!_database.GetCollectionNames().Contains(_collectionName))
            {
                try
                {
                    _database.CreateCollection(_collectionName);
                    var collection = _database.GetCollection(_collectionName);
                    collection.EnsureIndex(IndexKeys<EventBatch>.Ascending(b => b.SeqNo), IndexOptions.SetUnique(true));
                }
                catch { }
            }

            _host = WebApp.Start(_listenUri, a =>
            {
                GlobalHost.DependencyResolver.Register(typeof(EventStoreHub), () => new EventStoreHub(_database, _sequenceNumberGenerator, _collectionName));

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
}
