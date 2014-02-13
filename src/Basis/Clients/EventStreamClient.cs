using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Basis.Messages;
using Basis.Server;
using Microsoft.AspNet.SignalR.Client;
using NLog;

namespace Basis.Clients
{
    public class EventStreamClient : IDisposable
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        readonly JsonSerializer _serializer = new JsonSerializer();
        readonly IStreamHandler _streamHandler;
        readonly string _eventStoreListenUri;
        readonly Sequencer _sequencer;
        readonly Timer _periodicRecoveryTimer;
        readonly Timer _periodicSyncRequestTimer;
        readonly HubConnection _hubConnection;
        IHubProxy _eventStoreProxy;
        bool _started;

        public EventStreamClient(IStreamHandler streamHandler, string eventStoreListenUri)
        {
            _hubConnection = new HubConnection(eventStoreListenUri);
            _streamHandler = streamHandler;
            _eventStoreListenUri = eventStoreListenUri;
            _sequencer = new Sequencer(streamHandler);

            _periodicRecoveryTimer = new Timer(5000);
            _periodicRecoveryTimer.Elapsed += (o, ea) => RequestMissingEvents();
            _periodicRecoveryTimer.Start();

            _periodicSyncRequestTimer = new Timer(20000);
            _periodicSyncRequestTimer.Elapsed += (o, ea) => RequestPlayback();
            _periodicSyncRequestTimer.Start();
        }

        public void Start()
        {
            if (_started)
            {
                throw new InvalidOperationException("Event stream client has already been started! Cannot start event stream client twice!");
            }

            Log.Info("Starting event stream client for {0}", _eventStoreListenUri);

            _eventStoreProxy = _hubConnection.CreateHubProxy(typeof(EventStoreHub).Name);

            Handle<PlaybackEventBatch>("Publish", dto => DispatchToStreamHandler(dto));
            Handle<PlaybackEventBatch>("Accept", dto => DispatchToStreamHandler(dto));

            Log.Debug("Opening connection");
            _hubConnection.Start().Wait();

            RequestPlayback();

            _started = true;
        }

        void Handle<TDto>(string eventName, Action<TDto> onData)
        {
            _subscriptions.Add(_eventStoreProxy.On(eventName, onData));
        }

        void RequestPlayback()
        {
            var lastSequenceNumber = _streamHandler.GetLastSequenceNumber();
            Log.Info("Last seq no: {0}", lastSequenceNumber);

            _eventStoreProxy.Invoke("RequestPlayback", new RequestPlaybackArgs(lastSequenceNumber));
        }

        void RequestMissingEvents()
        {
            var missingSequenceNumbers = _sequencer.GetMissingSequenceNumbers();

            if (!missingSequenceNumbers.Any())
            {
                _sequencer.RunDispatcherForSafetysSake();
                return;
            }

            Log.Warn("Detected that {0} seq nos were missing - sending request for the first 1000", missingSequenceNumbers.Length);

            var first1000 = missingSequenceNumbers.OrderBy(s => s).Take(1000).ToArray();

            _eventStoreProxy.Invoke("RequestSpecificEvents", new RequestSpecificEventsArgs(first1000));
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
            _periodicRecoveryTimer.Stop();
            _periodicSyncRequestTimer.Stop();

            Log.Info("Stopping subscriptions");
            _subscriptions.ForEach(s => s.Dispose());
            _subscriptions.Clear();

            Log.Info("Shutting down event stream client for {0}", _eventStoreListenUri);
            _hubConnection.Dispose();

            Log.Info("Event stream client stopped");

            _periodicRecoveryTimer.Dispose();

            _started = false;
        }
    }
}
