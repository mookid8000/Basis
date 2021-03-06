﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Basis.Clients;
using Basis.Server;
using NUnit.Framework;
using Timer = System.Timers.Timer;

namespace Basis.Tests.Integration
{
    [TestFixture]
    public class ManyEventsAndAutomaticRecovery : MongoFixture
    {
        const string CollectionName = "events";
        EventStreamClient _eventStreamClient;
        EventStoreServer _eventStoreServer;
        InlineStreamHandler _inlineStreamHandler;
        EventStoreClient _eventStoreClient;

        protected override void DoSetUp()
        {
            var database = GetDatabase();

            _inlineStreamHandler = new InlineStreamHandler();
            var eventStoreListenUri = UrlHelper.GetNextLocalhostUrl();

            _eventStoreServer = Track(new EventStoreServer(database, CollectionName, eventStoreListenUri));
            _eventStreamClient = Track(new EventStreamClient(_inlineStreamHandler, eventStoreListenUri));
            _eventStoreClient = Track(new EventStoreClient(eventStoreListenUri));

            database.DropCollection(CollectionName);
        }

        //[TestCase(10)]
        //[TestCase(11)]
        //[TestCase(12)]
        //[TestCase(100)]
        //[TestCase(1000)]
        //[TestCase(2000)]
        //[TestCase(5000)]
        [TestCase(10000)]
        public void Yay(int totalNumberOfEvents)
        {
            var allMessagesReceived = new ManualResetEvent(false);

            _eventStoreServer.Start();
            _eventStoreClient.Start();

            var receivedNumbers = new ConcurrentQueue<int>();
            var storedEvents = 0;
            var clientStarted = false;

            using (var startTheClientTimer = new Timer(100))
            using (Every5s(() => Console.WriteLine("{0} events stored/{1} messages received", storedEvents, receivedNumbers.Count)))
            {
                // now, start the client
                _inlineStreamHandler.Handle(batch =>
                {
                    foreach (var evt in batch.OfType<SomeoneSaid>())
                    {
                        receivedNumbers.Enqueue(evt.MessageNumber);
                    }

                    if (receivedNumbers.Count >= totalNumberOfEvents)
                    {
                        allMessagesReceived.Set();
                    }
                });

                startTheClientTimer.Elapsed += delegate
                {
                    if (clientStarted) return;

                    // start the client when 1/2 of the events have been stored
                    if (storedEvents >= totalNumberOfEvents / 2)
                    {
                        clientStarted = true;

                        Console.WriteLine("{0} events stored - starting the client now...", storedEvents);
                        
                        _eventStreamClient.Start();
                    }
                };
                
                startTheClientTimer.Start();

                Enumerable.Range(0, totalNumberOfEvents)
                    .ToList()
                    .ForEach(i =>
                    {
                        _eventStoreClient.Save(new[]
                        {
                            new SomeoneSaid {MessageNumber = i}
                        }).Wait();

                        storedEvents++;
                    });

                allMessagesReceived.WaitOne();
            }

            Assert.That(receivedNumbers.Count, Is.EqualTo(totalNumberOfEvents));
            CollectionAssert.AreEqual(Enumerable.Range(0, totalNumberOfEvents), receivedNumbers);
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

            public async Task ProcessEvent(DeserializedEvent deserializedEvent)
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
            public int MessageNumber { get; set; }
        }
    }
}
