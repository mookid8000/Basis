using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Basis.MongoDb.Messages;
using Microsoft.AspNet.SignalR;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using NLog;

namespace Basis.MongoDb
{
    public class EventStoreHub : Hub
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        readonly MongoDatabase _database;
        readonly SequenceNumberGenerator _sequenceNumberGenerator;
        readonly string _collectionName;

        public EventStoreHub(MongoDatabase database, SequenceNumberGenerator sequenceNumberGenerator, string collectionName)
        {
            _database = database;
            _sequenceNumberGenerator = sequenceNumberGenerator;
            _collectionName = collectionName;
        }

        public async Task Save(EventBatchDto eventBatchToSave)
        {
            var seqNoForThisEvent = _sequenceNumberGenerator.GetNextSequenceNumber();

            Log.Debug("Inserting {0}... ", seqNoForThisEvent);

            _database.GetCollection<EventBatch>(_collectionName)
                .Insert(new EventBatch
                {
                    Events = eventBatchToSave.Events,
                    SeqNo = seqNoForThisEvent
                });

            await Clients.All.Publish(eventBatchToSave);
        }

        public async Task RequestPlayback(RequestPlaybackArgs args)
        {
            var currentSeqNo = args.CurrentSeqNo;

            var stopwatch = Stopwatch.StartNew();
            Log.Info("Playback requested by {0} from seq no {1}", Context.ConnectionId, currentSeqNo);
            var eventBatchesPlayedBack = 0;

            do
            {
                var eventBatches = _database.GetCollection<EventBatch>(_collectionName)
                    .Find(Query<EventBatch>.GT(e => e.SeqNo, currentSeqNo))
                    .SetSortOrder(SortBy<EventBatch>.Ascending(b => b.SeqNo))
                    .SetLimit(100)
                    .ToList();

                eventBatchesPlayedBack += eventBatches.Count;

                if (!eventBatches.Any()) break;

                Log.Debug("Delivering batches {0} to {1}",
                    string.Join(", ", eventBatches.Select(b => b.SeqNo), Context.ConnectionId));

                foreach (var batch in eventBatches)
                {
                    await Clients.Caller.Accept(new EventBatchDto
                    {
                        SeqNo = batch.SeqNo,
                        Events = batch.Events
                    });
                }
                
                currentSeqNo = eventBatches.Max(b => b.SeqNo);
            } while (true);

            var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            Log.Info("{0} events played back in {1:0.0} s - that's {2:0.0} events/s",
                eventBatchesPlayedBack, elapsedSeconds, eventBatchesPlayedBack/elapsedSeconds);
        }
    }
}