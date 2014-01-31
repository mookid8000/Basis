using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Basis.MongoDb;
using NUnit.Framework;

namespace Basis.Tests
{
    [TestFixture]
    public class ManyEvents : MongoFixture
    {
        const string CollectionName = "events";
        const string EventStoreListenUri = "http://localhost:3000";
        EventStreamClient _eventStreamClient;
        EventStore _eventStore;
        InlineStreamHandler _inlineStreamHandler;
        EventStoreClient _eventStoreClient;

        protected override void DoSetUp()
        {
            var database = GetDatabase();

            _inlineStreamHandler = new InlineStreamHandler();
            _eventStreamClient = Track(new EventStreamClient(_inlineStreamHandler, EventStoreListenUri));
            _eventStore = Track(new EventStore(database, CollectionName, EventStoreListenUri));
            _eventStoreClient = Track(new EventStoreClient(EventStoreListenUri));

            database.DropCollection(CollectionName);
        }

        class InlineStreamHandler : IStreamHandler
        {
            readonly List<Action<IEnumerable<object>>> _batchHandlers = new List<Action<IEnumerable<object>>>();
            long _lastSeqNo = -1;

            public void Handle(Action<IEnumerable<object>> batchHandler)
            {
                _batchHandlers.Add(batchHandler);
            }
            public long GetLastSequenceNumber()
            {
                return _lastSeqNo;
            }

            public async Task ProcessEvents(IEnumerable<object> events)
            {
                foreach (var handler in _batchHandlers)
                {
                    handler(events);
                }
            }

            public async Task Commit(long newLastSequenceNumber)
            {
                _lastSeqNo = newLastSequenceNumber;
            }
        }

        [TestCase(10)]
        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(2000)]
        [TestCase(5000)]
        [TestCase(10000)]
        public void Yay(int messageCount)
        {
            var allMessagesReceived = new ManualResetEvent(false);
            _eventStore.Start();
            _eventStoreClient.Start();

            Thread.Sleep(TimeSpan.FromSeconds(1));

            Enumerable.Range(0, messageCount)
                .ToList()
                .ForEach(i => _eventStoreClient.Save(new[]
                {
                    new SomeoneSaid {MessageNumber = i}
                }).Wait());

            // store some messages before spinning up the client
            Thread.Sleep(TimeSpan.FromSeconds(3));

            // now, start the client
            var receivedNumbers = new ConcurrentQueue<int>();

            _inlineStreamHandler.Handle(batch =>
            {
                foreach (var evt in batch.OfType<SomeoneSaid>())
                {
                    receivedNumbers.Enqueue(evt.MessageNumber);
                }

                if (receivedNumbers.Count >= messageCount)
                {
                    allMessagesReceived.Set();
                }
            });

            _eventStreamClient.Start();

            Thread.Sleep(TimeSpan.FromSeconds(1));

            using (Every5s(() => Console.WriteLine("{0} messages received", receivedNumbers.Count)))
            {
                allMessagesReceived.WaitOne();
            }

            Assert.That(receivedNumbers.Count, Is.EqualTo(messageCount));
            CollectionAssert.AreEqual(Enumerable.Range(0, messageCount), receivedNumbers);
        }

        class SomeoneSaid
        {
            public int MessageNumber { get; set; }
        }
    }
}
