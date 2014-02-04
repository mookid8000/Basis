﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Basis.MongoDb.Messages;
using Microsoft.AspNet.SignalR.Client;
using NLog;

namespace Basis.MongoDb
{
    public class EventStreamClient : IDisposable
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        readonly IStreamHandler _streamHandler;
        readonly string _eventStoreListenUri;
        readonly JsonSerializer _serializer = new JsonSerializer();
        readonly Sequencer _sequencer;

        long _lastSeqNo = -1;
        HubConnection _hubConnection;
        IHubProxy _eventStoreProxy;
        bool _started;

        public EventStreamClient(IStreamHandler streamHandler, string eventStoreListenUri)
        {
            _streamHandler = streamHandler;
            _eventStoreListenUri = eventStoreListenUri;
            _sequencer = new Sequencer(streamHandler);
        }

        public void Start()
        {
            if (_started)
            {
                throw new InvalidOperationException("Event stream client has already been started! Cannot start event stream client twice!");
            }

            _lastSeqNo = _streamHandler.GetLastSequenceNumber();
            
            Log.Info("Starting event stream client for {0}", _eventStoreListenUri);

            _hubConnection = new HubConnection(_eventStoreListenUri);
            _eventStoreProxy = _hubConnection.CreateHubProxy(typeof(EventStoreHub).Name);

            _eventStoreProxy.On("Publish", (PlaybackEventBatch dto) => DispatchToStreamHandler(dto));
            _eventStoreProxy.On("Accept", (PlaybackEventBatch dto) => DispatchToStreamHandler(dto));

            Log.Debug("Opening connection");
            _hubConnection.Start().Wait();

            Log.Info("Last seq no: {0}", _lastSeqNo);
            EnsureWeCatchUp();

            _started = true;
        }

        void EnsureWeCatchUp()
        {
            _eventStoreProxy.Invoke("RequestPlayback",
                new RequestPlaybackArgs {CurrentSeqNo = _streamHandler.GetLastSequenceNumber()});
        }

        async Task DispatchToStreamHandler(PlaybackEventBatch batch)
        {
            Log.Debug("Dispatching {0}-{1}", batch.Events.First().SeqNo, batch.Events.Last().SeqNo);

            try
            {
                foreach (var evt in batch.Events)
                {
                    var body = _serializer.Deserialize(evt.Body);

                    await _sequencer.Enqueue(new DeserializedEvent(evt.SeqNo, body));
                }
            }
            catch (Exception exception)
            {
                Log.Warn(exception);
            }
        }

        public void Dispose()
        {
            if (_hubConnection != null)
            {
                Log.Info("Shutting down event stream client for {0}", _eventStoreListenUri);
                _hubConnection.Dispose();

                _hubConnection = null;
                Log.Info("Event stream client stopped");
            }

            _started = false;
        }
    }
}
