using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Basis.MongoDb;
using Basis.MongoDb.Clients;
using Basis.MongoDb.Server;
using NUnit.Framework;

namespace Basis.Tests.Integration
{
    [TestFixture]
    public class ServerCanResumeTest : MongoFixture
    {
        [Test]
        public async Task YesWeCan()
        {
            const string collectionName = "resume_events";
            var uri = UrlHelper.GetNextLocalhostUrl();
            var accumulatingHandler = new AccumulatingHandler();
            var mongoDatabase = GetDatabase();
            
            mongoDatabase.DropCollection(collectionName);

            using (var server = new EventStoreServer(mongoDatabase, collectionName, uri))
            {
                server.Start();

                using (var store = new EventStoreClient(uri))
                {
                    store.Start();

                    using (var view = new EventStreamClient(accumulatingHandler, uri))
                    {
                        view.Start();

                        await store.Save(new[] {new AnEvent()});
                    }
                }
            }

            using (var server = new EventStoreServer(mongoDatabase, collectionName, uri))
            {
                server.Start();

                using (var store = new EventStoreClient(uri))
                {
                    store.Start();

                    using (var view = new EventStreamClient(accumulatingHandler, uri))
                    {
                        view.Start();

                        await store.Save(new[] {new AnEvent()});

                        Thread.Sleep(TimeSpan.FromSeconds(3));
                    }
                }
            }

            var processedSequenceNumbers = accumulatingHandler.SequenceNumbers.ToList();

            Console.WriteLine("Got numbers: {0}", string.Join(", ", processedSequenceNumbers));

            Assert.That(processedSequenceNumbers[0], Is.EqualTo(1));
            Assert.That(processedSequenceNumbers[1], Is.EqualTo(2));
        }

        class AnEvent
        {
            public AnEvent()
            {
                Id = Guid.NewGuid();
            }
            public Guid Id { get; protected set; }
        }

        class AccumulatingHandler : IStreamHandler
        {
            readonly List<long> _sequenceNumbers = new List<long>();

            public IEnumerable<long> SequenceNumbers
            {
                get { return _sequenceNumbers.ToList(); }
            }

            public long GetLastSequenceNumber()
            {
                return _sequenceNumbers.Any()
                    ? _sequenceNumbers.Max()
                    : 0;
            }

            public async Task ProcessEvent(DeserializedEvent deserializedEvent)
            {
                var seqNo = deserializedEvent.SeqNo;
                
                Console.WriteLine("***************************************** Processing {0}", seqNo);
                
                _sequenceNumbers.Add(seqNo);

                Console.WriteLine("***************************************** Finished processing {0}", seqNo);
            }
        }
    }
}