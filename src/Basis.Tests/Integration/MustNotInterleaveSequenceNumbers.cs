using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Basis.Clients;
using Basis.Server;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.Builders;
using NUnit.Framework;

namespace Basis.Tests.Integration
{
    [TestFixture]
    public class MustNotInterleaveSequenceNumbers : MongoFixture
    {
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000, Explicit = true)]
        public void ConcurrentlySavingEventsDoesNotResultInInterleavedSequenceNumbers(int numBatches)
        {
            var url = UrlHelper.GetNextLocalhostUrl();
            var mongoDatabase = GetDatabase();
            mongoDatabase.DropCollection("events");

            using (var server = new EventStoreServer(mongoDatabase, "events", url))
            {
                server.Start();

                using (var store = new EventStoreClient(url))
                {
                    store.Start();

                    var events = Enumerable.Range(0, numBatches)
                        .Select(i => new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }
                            .Select(i2 =>
                                new
                                {
                                    I1 = i,
                                    I2 = i2
                                })
                            .ToList())
                        .ToList();

                    var tasks = events
                        .Select(async evnt => await store.Save(evnt))
                        .ToArray();

                    Task.WaitAll(tasks);
                }
            }

            var savedBatches = mongoDatabase.GetCollection<PersistenceEventBatchSlice>("events")
                .FindAll().SetSortOrder(SortBy.Ascending("FirstSeqNo"))
                .ToList();

            Assert.That(savedBatches.Count, Is.EqualTo(numBatches));

            foreach (var batch in savedBatches)
            {
                var seqNos = batch.Events.Select(e => (int)e.SeqNo).ToList();

                CollectionAssert.AreEquivalent(Enumerable.Range(seqNos.First(), seqNos.Count).ToList(), seqNos,
                    "Event batch sequence numbers did not constitute a non-broken sequence of seq nos");
            }
        }

        [BsonIgnoreExtraElements]
        class PersistenceEventBatchSlice
        {
            public List<PersistenceEventSlice> Events { get; set; }
        }

        [BsonIgnoreExtraElements]
        class PersistenceEventSlice
        {
            public long SeqNo { get; set; }
        }
    }
}