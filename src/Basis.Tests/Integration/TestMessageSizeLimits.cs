using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Basis.Clients;
using Basis.Server;
using NUnit.Framework;

namespace Basis.Tests.Integration
{
    [TestFixture]
    public class TestMessageSizeLimits : MongoFixture
    {
        //[TestCase(1024 * 32)]
        //[TestCase(1024 * 33)]
        //[TestCase(1024 * 34)]
        //[TestCase(1024 * 35)]
        //[TestCase(1024 * 36)]
        //[TestCase(1024 * 37)]
        //[TestCase(1024 * 38)]
        //[TestCase(1024 * 39)]
        //[TestCase(1024 * 40)]
        [TestCase(1024 * 64)]
        [TestCase(1024 * 128)]
        [TestCase(1024 * 256)]
        [TestCase(1024 * 512)]
        [TestCase(1024 * 1024)]
        public async Task RawSize(int numberOfBytes)
        {
            var url = UrlHelper.GetNextLocalhostUrl();
            var database = GetDatabase("basis_test");
            database.DropCollection("events");

            using (var server = new EventStoreServer(database, "events", url))
            {
                server.Start();

                using (var store = new EventStoreClient(url))
                {
                    store.Start();

                    var handler = new GenericBlockingSingleEventStreamHandler<RawBytesEvent>(TimeSpan.FromSeconds(numberOfBytes / 1024));

                    using (var client = new EventStreamClient(handler, url))
                    {
                        client.Start();

                        var payload = Enumerable.Repeat((byte)34, numberOfBytes).ToArray();

                        await store.Save(new[] { new RawBytesEvent { Payload = payload } });

                        var rawBytesEvent = handler.Event;
                        Assert.That(rawBytesEvent.Payload.Length, Is.EqualTo(numberOfBytes), "Could not preserve load when transferring {0} kB", numberOfBytes / 1024);
                    }
                }
            }
        }
    }

    class RawBytesEvent
    {
        public byte[] Payload { get; set; }
    }

    public class GenericBlockingSingleEventStreamHandler<TEventToWaitFor> : IStreamHandler
        where TEventToWaitFor : class
    {
        readonly TimeSpan _timeout;
        readonly ManualResetEvent _gotEvent = new ManualResetEvent(false);
        long _seqNo;
        TEventToWaitFor _event;

        public GenericBlockingSingleEventStreamHandler(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        public long GetLastSequenceNumber()
        {
            return _seqNo;
        }

        public async Task ProcessEvent(DeserializedEvent deserializedEvent)
        {
            if (deserializedEvent.Event is TEventToWaitFor)
            {
                Event = (TEventToWaitFor)deserializedEvent.Event;
            }

            _seqNo = deserializedEvent.SeqNo;
        }

        public TEventToWaitFor Event
        {
            get
            {
                Console.WriteLine("Getting (possibly blocking and waiting for) {0}", typeof(TEventToWaitFor).Name);

                if (!_gotEvent.WaitOne(_timeout))
                {
                    throw new TimeoutException(string.Format("Waited {0} for {1}, but none came!", _timeout, typeof(TEventToWaitFor).Name));
                }

                return _event;
            }
            private set
            {
                if (value == null) return;

                Console.WriteLine("Received event {0}", value.GetType().Name);
                _event = value;
                _gotEvent.Set();
            }
        }
    }
}