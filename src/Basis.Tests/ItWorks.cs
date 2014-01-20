using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Basis.MongoDb;
using NUnit.Framework;

namespace Basis.Tests
{
    [TestFixture]
    public class ItWorks : MongoFixture
    {
        EventStream _eventStream;
        EventStore _eventStore;
        InlineStreamHandler _inlineStreamHandler;
        const string CollectionName = "events";

        protected override void DoSetUp()
        {
            var database = GetDatabase();

            _inlineStreamHandler = new InlineStreamHandler();
            _eventStream = Track(new EventStream(database, CollectionName, _inlineStreamHandler));
            _eventStore = Track(new EventStore(database, CollectionName));

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

            public void ProcessEvents(IEnumerable<object> events)
            {
                foreach (var handler in _batchHandlers)
                {
                    handler(events);
                }
            }

            public void Commit(long newLastSequenceNumber)
            {
                _lastSeqNo = newLastSequenceNumber;
            }
        }

        [Test]
        public void Yay()
        {
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

            _eventStream.Start();
            _eventStore.Start();

            Thread.Sleep(TimeSpan.FromSeconds(1));

            _eventStore.Save(new[]
            {
                new SomeoneSaid {Value = "hello"},
                new SomeoneSaid {Value = "world"}
            });

            Thread.Sleep(TimeSpan.FromSeconds(1));

            Assert.That(greeting.ToString(), Is.EqualTo("hello world"));
        }

        class SomeoneSaid
        {
            public string Value { get; set; }
        }
    }
}
