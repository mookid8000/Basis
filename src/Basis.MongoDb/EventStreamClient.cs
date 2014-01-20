using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNet.SignalR.Client;

namespace Basis.MongoDb
{
    public class EventStreamClient : IDisposable
    {
        readonly IStreamHandler _streamHandler;
        readonly string _eventStoreListenUri;

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
            Console.WriteLine("Last seq no: {0}", _lastSeqNo);

            _hubConnection = new HubConnection(_eventStoreListenUri);
            _eventStoreProxy = _hubConnection.CreateHubProxy("EventStoreHub");

            _eventStoreProxy.On("Publish", (EventBatchDto dto) => DispatchToStreamHandler(dto));

            _hubConnection.Start().Wait();
        }

        async Task DispatchToStreamHandler(EventBatchDto dto)
        {
            var events = dto.Events;

            try
            {
                await _streamHandler.ProcessEvents(events);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }

        public void Dispose()
        {
        }
    }
}
