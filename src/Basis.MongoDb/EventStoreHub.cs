using System;
using System.Linq;
using System.Threading.Tasks;
using Basis.MongoDb.Messages;
using Microsoft.AspNet.SignalR;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Basis.MongoDb
{
    public class EventStoreHub : Hub
    {
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

        public async Task RequestPlayback(RequestPlaybackArgs args)
        {
            var currentSeqNo = args.CurrentSeqNo;

            Console.WriteLine("Playback requested by {0} from seq no {1}", Context.ConnectionId, currentSeqNo);

            do
            {
                var batches = _database.GetCollection<EventBatch>(_collectionName)
                    .Find(Query<EventBatch>.GT(e => e.SeqNo, currentSeqNo))
                    .SetSortOrder(SortBy<EventBatch>.Ascending(b => b.SeqNo))
                    .SetLimit(100)
                    .ToList();

                if (!batches.Any()) break;

                foreach (var batch in batches)
                {
                    Console.WriteLine("Delivering batch {0} to {1}", batch.SeqNo, Context.ConnectionId);

                    await Clients.Caller.Accept(new EventBatchDto
                    {
                        SeqNo = batch.SeqNo,
                        Events = batch.Events
                    });
                }
                
                currentSeqNo = batches.Max(b => b.SeqNo);
            } while (true);
        }
    }
}