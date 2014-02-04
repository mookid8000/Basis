using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Basis.MongoDb;
using NUnit.Framework;

namespace Basis.Tests
{
    [TestFixture]
    public class CanCatchUp : MongoFixture
    {
        const string CollectionName = "events";
        const string EventStoreListenUri = "http://localhost:3000";
        EventStreamClient _eventStreamClient;
        EventStoreServer _eventStoreServer;
        InlineStreamHandler _inlineStreamHandler;
        EventStoreClient _eventStoreClient;

        protected override void DoSetUp()
        {
            var database = GetDatabase();

            _inlineStreamHandler = new InlineStreamHandler();
            _eventStreamClient = Track(new EventStreamClient(_inlineStreamHandler, EventStoreListenUri));
            _eventStoreServer = Track(new EventStoreServer(database, CollectionName, EventStoreListenUri));
            _eventStoreClient = Track(new EventStoreClient(EventStoreListenUri));

            database.DropCollection(CollectionName);
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

        [Test]
        public void Yay()
        {
            _eventStoreServer.Start();
            _eventStoreClient.Start();

            Thread.Sleep(TimeSpan.FromSeconds(1));

            _eventStoreClient.Save(new[]
            {
                new SomeoneSaid {Value = "hello"},
                new SomeoneSaid {Value = "world"}
            })
            .Wait();

            Thread.Sleep(TimeSpan.FromSeconds(1));

            // now, start the client
            var greeting = new StringBuilder();

            _inlineStreamHandler.Handle(batch =>
            {
                foreach (var evt in batch.OfType<SomeoneSaid>())
                {
                    if (greeting.Length == 0)
                    {
                        greeting.Append(evt.Value);
                    }
                    else
                    {
                        greeting.Append(" " + evt.Value);
                    }
                }
            });

            _eventStreamClient.Start();

            Thread.Sleep(TimeSpan.FromSeconds(3));

            Assert.That(greeting.ToString(), Is.EqualTo("hello world"));
        }

        class SomeoneSaid
        {
            public string Value { get; set; }
        }
    }
}
