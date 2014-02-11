﻿using System;
using System.Linq;
using Basis.MongoDb.Persistence;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin.Cors;
using Microsoft.Owin.Hosting;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Linq;
using NLog;
using Owin;

namespace Basis.MongoDb.Server
{
    public class EventStoreServer : IDisposable
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        readonly string _listenUri;
        readonly string _collectionName;
        readonly MongoDatabase _database;
        readonly SequenceNumberGenerator _sequenceNumberGenerator;

        IDisposable _host;

        bool _started;

        public EventStoreServer(MongoDatabase database, string collectionName, string listenUri)
        {
            _database = database;
            _collectionName = collectionName;
            _listenUri = listenUri;
            _sequenceNumberGenerator = new SequenceNumberGenerator(0);
        }

        public void Start()
        {
            if (_started)
            {
                throw new InvalidOperationException(string.Format("Event store server has already been started! It cannot be started twice"));
            }

            Log.Info("Starting event store server on {0}", _listenUri);

            if (!_database.GetCollectionNames().Contains(_collectionName))
            {
                Log.Info("Collection {0} does not exist - will initialize it now", _collectionName);
                try
                {
                    _database.CreateCollection(_collectionName);
                    var collection = _database.GetCollection(_collectionName);
                    collection.EnsureIndex(IndexKeys.Ascending("Events.SeqNo"), IndexOptions.SetUnique(true));
                }
                catch { }

                var lastEvent = _database
                    .GetCollection<PersistenceEventBatch>(_collectionName)
                    .FindAll()
                    .SetSortOrder(SortBy.Descending("FirstSeqNo"))
                    .SetLimit(1)
                    .FirstOrDefault();

                if (lastEvent != null)
                {
                    var mostRecentEventNumber = lastEvent.Events.Max(e => e.SeqNo);
                    
                    _sequenceNumberGenerator.StartWith(mostRecentEventNumber+1);
                }
            }

            Log.Debug("Starting OWIN host with SignalR hub");
            _host = WebApp.Start(_listenUri, a =>
            {
                a.UseCors(CorsOptions.AllowAll);
                
                var hubConfiguration = new HubConfiguration();
                hubConfiguration.Resolver.Register(typeof(EventStoreHub), () => new EventStoreHub(_database, _sequenceNumberGenerator, _collectionName));
                hubConfiguration.EnableDetailedErrors = true;

                a.RunSignalR(hubConfiguration);
            });

            Log.Info("Event store server started");
            _started = true;
        }

        public void Dispose()
        {
            if (_host != null)
            {
                Log.Info("Shutting down event store server on {0}", _listenUri);
                _host.Dispose();
                
                _host = null;
                Log.Info("Event store server stopped");
            }

            _started = false;
        }
    }
}