using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Basis.MongoDb.Messages;
using Basis.MongoDb.Persistence;
using Microsoft.AspNet.SignalR;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using NLog;

namespace Basis.MongoDb.Server
{
    public class EventStoreHub : Hub
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        readonly SequenceNumberGenerator _sequenceNumberGenerator;
        readonly MongoCollection<PersistenceEventBatch> _eventsCollection;

        public EventStoreHub(MongoDatabase database, SequenceNumberGenerator sequenceNumberGenerator, string collectionName)
        {
            _sequenceNumberGenerator = sequenceNumberGenerator;
            _eventsCollection = database.GetCollection<PersistenceEventBatch>(collectionName);
        }

        public async Task Save(EventBatchToSave eventBatchToSave)
        {
            var events = eventBatchToSave.Events
                .Select(bytes => new PersistenceEvent
                {
                    Body = bytes,
                    SeqNo = _sequenceNumberGenerator.GetNextSequenceNumber(),
                    Meta = new Dictionary<string, string>()
                })
                .ToList();

            Log.Debug("Inserting {0}-{1}... ", events.First().SeqNo, events.Last().SeqNo);

            _eventsCollection.Insert(new PersistenceEventBatch(events));

            var playbackEvents = events.Select(e => new PlaybackEvent(e.SeqNo, e.Body));
            var playbackEventBatch = new PlaybackEventBatch(playbackEvents);

            await Clients.All.Publish(playbackEventBatch);
        }

        public async Task RequestPlayback(RequestPlaybackArgs args)
        {
            var currentSeqNo = args.CurrentSeqNo;

            var stopwatch = Stopwatch.StartNew();
            Log.Info("Playback requested by {0} from seq no {1}", Context.ConnectionId, currentSeqNo);
            var eventBatchesPlayedBack = 0;

            try
            {
                do
                {
                    var minSeqNo = currentSeqNo;
                    
                    var eventBatches = _eventsCollection
                        .Find(Query.GT("Events.SeqNo", minSeqNo))
                        .SetSortOrder(SortBy.Ascending("FirstSeqNo"))
                        .SetLimit(100)
                        .ToList();

                    eventBatchesPlayedBack += eventBatches.Count;

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
                } while (true);

                var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

                Log.Info("{0} events played back in {1:0.0} s - that's {2:0.0} events/s",
                    eventBatchesPlayedBack, elapsedSeconds, eventBatchesPlayedBack/elapsedSeconds);
            }
            catch (Exception exception)
            {
                Log.WarnException("An exception occurred while attempting to play back events for client", exception);
            }
        }
    }
}