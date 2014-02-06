using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Basis.MongoDb;
using Basis.MongoDb.Clients;
using Basis.MongoDb.Server;
using NUnit.Framework;

namespace Basis.Tests.Integration
{
    [TestFixture]
    public class DropEvent : MongoFixture
    {
        const string CollectionName = "events";
        EventStreamClient _eventStreamClient;
        EventStoreServer _eventStoreServer;
        InlineStreamHandler _inlineStreamHandler;
        EventStoreClient _eventStoreClient;

        protected override void DoSetUp()
        {
            var database = GetDatabase();

            var eventStoreListenUri = UrlHelper.GetNextLocalhostUrl();

            _inlineStreamHandler = new InlineStreamHandler();
            _eventStreamClient = Track(new EventStreamClient(_inlineStreamHandler, eventStoreListenUri));
            _eventStoreServer = Track(new EventStoreServer(database, CollectionName, eventStoreListenUri));
            _eventStoreClient = Track(new EventStoreClient(eventStoreListenUri));

            database.DropCollection(CollectionName);
        }

        [Test]
        public void Yay()
        {
            var greeting = new StringBuilder();
            var firstCall = true;

            _inlineStreamHandler.Handle(batch =>
            {
                // throw on first attempt to dispatch the event
                if (firstCall)
                {
                    firstCall = false;
                    throw new ApplicationException("oh noooooes, we'll lose the event now!!");
                }

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

            _eventStoreServer.Start();

            _eventStreamClient.Start();
            _eventStoreClient.Start();

            Thread.Sleep(TimeSpan.FromSeconds(1));

            _eventStoreClient.Save(new[]
            {
                new SomeoneSaid {Value = "hello"},
                new SomeoneSaid {Value = "world"}
            }).Wait();

            Thread.Sleep(TimeSpan.FromSeconds(1));

            Assert.That(greeting.ToString(), Is.EqualTo("hello world"));
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

        class SomeoneSaid
        {
            public string Value { get; set; }
        }
    }
}
