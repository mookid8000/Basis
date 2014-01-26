using System;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using MongoDB.Driver;

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
    }
}