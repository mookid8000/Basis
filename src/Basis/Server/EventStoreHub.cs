using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Basis.Messages;
using Basis.Persistence;
using Microsoft.AspNet.SignalR;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using NLog;
using Timer = System.Timers.Timer;

namespace Basis.Server
{
    public class EventStoreHub : Hub
    {
        private const string StreamClientsGroupName = "stream_clients";
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        readonly SequenceNumberGenerator _sequenceNumberGenerator;
        readonly MongoCollection<PersistenceEventBatch> _eventsCollection;
        readonly ConcurrentBag<string> _indexedPaths = new ConcurrentBag<string>();

        public EventStoreHub(MongoDatabase database, SequenceNumberGenerator sequenceNumberGenerator, string collectionName)
        {
            _sequenceNumberGenerator = sequenceNumberGenerator;
            _eventsCollection = database.GetCollection<PersistenceEventBatch>(collectionName);
        }

        public async Task Save(EventBatchToSave eventBatchToSave)
        {
            var events = new List<PersistenceEvent>();

            using (var generator = _sequenceNumberGenerator.GetGenerator())
            {
                events.AddRange(eventBatchToSave.Events
                    .Select(evnt => new PersistenceEvent
                    {
                        Body = evnt.Body,
                        SeqNo = generator.GetNextSequenceNumber(),
                        Meta = evnt.Meta ?? new Dictionary<string, string>()
                    }));
            }

            Log.Debug("Inserting {0}-{1}... ", events.First().SeqNo, events.Last().SeqNo);

            _eventsCollection.Insert(new PersistenceEventBatch(events), WriteConcern.Acknowledged);

            var playbackEvents = events.Select(e => new PlaybackEvent(e.SeqNo, e.Body));
            var playbackEventBatch = new PlaybackEventBatch(playbackEvents);

            await Clients.Group(StreamClientsGroupName).Publish(playbackEventBatch);

            var allKeysInThisBatch = events.SelectMany(e => e.Meta.Keys).Distinct();

            foreach (var key in allKeysInThisBatch)
            {
                if (_indexedPaths.Contains(key)) continue;

                _eventsCollection.EnsureIndex(IndexKeys.Ascending(string.Format("Events.Meta.{0}", key)));
                _indexedPaths.Add(key);
            }
        }

        public async Task RequestSpecificEvents(RequestSpecificEventsArgs args)
        {
            await Groups.Add(Context.ConnectionId, StreamClientsGroupName);

            var stopwatch = Stopwatch.StartNew();

            var eventNumbers = args.EventNumbers;

            Log.Info("Specific events requested by {0}: {1}", Context.ConnectionId, string.Join(", ", eventNumbers));

            try
            {
                var specificEvents = _eventsCollection
                    .Find(Query.In("Events.SeqNo", eventNumbers.Select(n => (BsonValue) n)))
                    .ToList()
                    .SelectMany(b => b.Events)
                    .Where(e => eventNumbers.Contains(e.SeqNo))
                    .Select(e => new PlaybackEvent(e.SeqNo, e.Body))
                    .ToList();

                var playbackEventBatch = new PlaybackEventBatch(specificEvents);

                await Clients.Caller.Accept(playbackEventBatch);

                var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

                Log.Info("{0} events played back in {1:0.0} s - that's {2:0.0} events/s",
                    specificEvents.Count, elapsedSeconds, specificEvents.Count/elapsedSeconds);
            }
            catch (Exception exception)
            {
                Log.WarnException("An exception occurred while attempting to retrieve specific events for client", exception);
            }
        }

        public async Task Subscribe()
        {
            await Groups.Add(Context.ConnectionId, StreamClientsGroupName);
        }
        public async Task Unsubscribe()
        {
            await Groups.Remove(Context.ConnectionId, StreamClientsGroupName);
        }

        public async Task RequestPlayback(RequestPlaybackArgs args)
        {
            var currentSeqNo = args.CurrentSeqNo;

            var stopwatch = Stopwatch.StartNew();
            var connectionId = Context.ConnectionId;

            Log.Info("Playback requested by {0} from seq no {1}", connectionId, currentSeqNo);
            var eventsPlayedBack = 0L;

            try
            {
                using (var logTimer = new Timer(10000))
                {
                    logTimer.Elapsed += (o, ea) =>
                    {
                        var currentValueOfEventsPlayedBack = Interlocked.Read(ref eventsPlayedBack);
                        Log.Debug("{0} events played back to {1}", currentValueOfEventsPlayedBack, connectionId);
                    };
                    logTimer.Start();

                    do
                    {
                        var minSeqNo = currentSeqNo;

                        var eventBatches = _eventsCollection
                            .Find(Query.GT("Events.SeqNo", minSeqNo))
                            .SetSortOrder(SortBy.Ascending("FirstSeqNo"))
                            .SetLimit(100)
                            .ToList();

                        if (!eventBatches.Any()) break;

                        var playbackEvents = eventBatches
                            .SelectMany(e => e.Events)
                            .Where(e => e.SeqNo > minSeqNo)
                            .OrderBy(e => e.SeqNo)
                            .Select(e => new PlaybackEvent(e.SeqNo, e.Body))
                            .Partition(20)
                            .ToList();

                        foreach (var batch in playbackEvents)
                        {
                            var playbackEventBatch = new PlaybackEventBatch(batch);

                            await Clients.Caller.Accept(playbackEventBatch);

                            currentSeqNo = batch.Max(b => b.SeqNo);
                        }

                        Interlocked.Add(ref eventsPlayedBack, playbackEvents.Count);
                    } while (true);
                }

                var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

                Log.Info("{0} events played back in {1:0.0} s - that's {2:0.0} events/s",
                    eventsPlayedBack, elapsedSeconds, eventsPlayedBack/elapsedSeconds);

                Log.Info("Subscribing {0} to RT events now", connectionId);

                await Subscribe();
            }
            catch (Exception exception)
            {
                Log.WarnException("An exception occurred while attempting to play back events for client", exception);
            }
        }
    }
}