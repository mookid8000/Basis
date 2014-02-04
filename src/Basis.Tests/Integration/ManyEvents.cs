using System;
using System.Collections.Concurrent;
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
    public class ManyEvents : MongoFixture
    {
        const string CollectionName = "events";
        const string EventStoreListenUriFormatString = "http://localhost:{0}";
        EventStreamClient _eventStreamClient;
        EventStoreServer _eventStoreServer;
        InlineStreamHandler _inlineStreamHandler;
        EventStoreClient _eventStoreClient;

        protected override void DoSetUp()
        {
            var database = GetDatabase();

            _inlineStreamHandler = new InlineStreamHandler();
            var eventStoreListenUri = GetNewUri();

            _eventStoreServer = Track(new EventStoreServer(database, CollectionName, eventStoreListenUri));
            _eventStreamClient = Track(new EventStreamClient(_inlineStreamHandler, eventStoreListenUri));
            _eventStoreClient = Track(new EventStoreClient(eventStoreListenUri));

            database.DropCollection(CollectionName);
        }

        int portNumber = 3000;
        string GetNewUri()
        {
            return string.Format(EventStoreListenUriFormatString, portNumber++);
        }

        class InlineStreamHandler : IStreamHandler
        {
            readonly List<Action<IEnumerable<object>>> _batchHandlers = new List<Action<IEnumerable<object>>>();
            long _lastSeqNo;

            public void Handle(Action<IEnumerable<object>> batchHandler)
            {
                _batchHandlers.Add(batchHandler);
            }
            public long GetLastSequenceNumber()
            {
                return _lastSeqNo;
            }

            public async Task ProcessEvents(DeserializedEvent deserializedEvent)
            {
                foreach (var handler in _batchHandlers)
                {
                    handler(new[] { deserializedEvent.Event });
                }

                _lastSeqNo = deserializedEvent.SeqNo;
            }
        }

        //[TestCase(10)]
        //[TestCase(11)]
        //[TestCase(12)]
        //[TestCase(100)]
        //[TestCase(1000)]
        [TestCase(2000)]
        //[TestCase(5000)]
        //[TestCase(10000)]
        public void Yay(int messageCount)
        {
            var allMessagesReceived = new ManualResetEvent(false);

            _eventStoreServer.Start();

            Thread.Sleep(TimeSpan.FromSeconds(1));

            _eventStoreClient.Start();

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
