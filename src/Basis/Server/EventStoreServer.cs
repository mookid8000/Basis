﻿using System;
using System.Linq;
using System.Timers;
using Basis.Persistence;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin.Cors;
using Microsoft.Owin.Hosting;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using NLog;
using Owin;

namespace Basis.Server
{
    public class EventStoreServer : IDisposable
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        readonly SequenceNumberGenerator _sequenceNumberGenerator = new SequenceNumberGenerator();
        readonly Timer _statsTimer = new Timer(60000);
        readonly Stats _stats = new Stats();
        readonly string _listenUri;
        readonly string _collectionName;
        readonly MongoDatabase _database;

        IDisposable _host;

        bool _started;

        public EventStoreServer(MongoDatabase database, string collectionName, string listenUri)
        {
            _database = database;
            _collectionName = collectionName;
            _listenUri = listenUri;

            _statsTimer.Elapsed += (o, ea) => LogStats();
        }

        void LogStats()
        {
            var playedBackEvents = _stats.GetPlayedBackEvents();
            var savedEventBatches = _stats.GetSavedEventBatches();
            var specificallyRequestedEvents = _stats.GetSpecificallyRequestedEvents();
            
            Log.Info("Event batches saved: {0} (last {1})", savedEventBatches.Count, savedEventBatches.Elapsed);
            Log.Info("Events played back: {0} (last {1})", playedBackEvents.Count, playedBackEvents.Elapsed);
            Log.Info("Events specifically requested: {0} (last {1})", specificallyRequestedEvents.Count, specificallyRequestedEvents.Elapsed);
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
                    collection.EnsureIndex(IndexKeys.Ascending("FirstSeqNo"), IndexOptions.SetUnique(true));
                    collection.EnsureIndex(IndexKeys.Ascending("Events.SeqNo"), IndexOptions.SetUnique(true));
                }
                catch { }
            }

            var lastEvent = _database
                .GetCollection<PersistenceEventBatch>(_collectionName)
                .FindAll()
                .SetSortOrder(SortBy.Descending("FirstSeqNo"))
                .SetLimit(1)
                .FirstOrDefault();

            if (lastEvent != null)
            {
                var mostRecentEventNumber = lastEvent.Events.Max(e => e.SeqNo);

                Log.Info("Initializing seq no generator to {0}", mostRecentEventNumber);

                _sequenceNumberGenerator.StartWith(mostRecentEventNumber);
            }

            Log.Debug("Starting OWIN host with SignalR hub");
            _host = WebApp.Start(_listenUri, a =>
            {
                a.UseCors(CorsOptions.AllowAll);

                var resolver = new DefaultDependencyResolver();
                resolver.Register(typeof(EventStoreHub), () => new EventStoreHub(_database, _sequenceNumberGenerator, _collectionName, _stats));

                var hubConfiguration = new HubConfiguration
                {
                    EnableDetailedErrors = Config.CurrentBuildConfig == Config.BuildConfig.Debug,
                    EnableJSONP = true,
                    Resolver = resolver,
                };

                a.RunSignalR(hubConfiguration);
            });

            Log.Info("Event store server started");
            _stats.Reset();
            _statsTimer.Start();
            _started = true;
        }

        public void Dispose()
        {
            _statsTimer.Dispose();

            if (_host != null)
            {
                Log.Info("Shutting down event store server on {0}", _listenUri);
                _host.Dispose();

                _host = null;
                Log.Info("Event store server stopped");
            }

            _started = false;

            LogStats();
        }
    }
}
