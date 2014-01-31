using System;
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

        long _lastSeqNo = -1;
        HubConnection _hubConnection;
        IHubProxy _eventStoreProxy;

        public EventStreamClient(IStreamHandler streamHandler, string eventStoreListenUri)
        {
            _streamHandler = streamHandler;
            _eventStoreListenUri = eventStoreListenUri;
        }

        public void Start()
        {
            _lastSeqNo = _streamHandler.GetLastSequenceNumber();
            
            Log.Info("Last seq no: {0}", _lastSeqNo);

            _hubConnection = new HubConnection(_eventStoreListenUri);
            _eventStoreProxy = _hubConnection.CreateHubProxy("EventStoreHub");

            _eventStoreProxy.On("Publish", (EventBatchDto dto) => DispatchToStreamHandler(dto));
            _eventStoreProxy.On("Accept", (EventBatchDto dto) => DispatchToStreamHandler(dto));

            _hubConnection.Start().Wait();

            EnsureWeCatchUp();
        }

        void EnsureWeCatchUp()
        {
            _eventStoreProxy.Invoke("RequestPlayback",
                new RequestPlaybackArgs {CurrentSeqNo = _streamHandler.GetLastSequenceNumber()});
        }

        async Task DispatchToStreamHandler(EventBatchDto dto)
        {
            Log.Debug("Dispatching {0}", dto.SeqNo);

            try
            {
                var events = _serializer.Deserialize(dto.Events);

                if (!(events is object[]))
                {
                    throw new ArgumentException(string.Format(@"Sorry! The following JSON object was not an object[]: {0}", _serializer.GetStringRepresentationSafe(dto.Events)));
                }
                await _streamHandler.ProcessEvents((object[])events);
            }
            catch (Exception exception)
            {
                Log.Warn(exception);
            }
        }

        public void Dispose()
        {
        }
    }
}
