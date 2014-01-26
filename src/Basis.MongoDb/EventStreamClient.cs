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
            Console.WriteLine("Last seq no: {0}", _lastSeqNo);

            _hubConnection = new HubConnection(_eventStoreListenUri);
            _eventStoreProxy = _hubConnection.CreateHubProxy("EventStoreHub");

            _eventStoreProxy.On("Publish", (EventBatchDto dto) => DispatchToStreamHandler(dto));

            _hubConnection.Start().Wait();
        }

        async Task DispatchToStreamHandler(EventBatchDto dto)
        {
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
                Console.WriteLine(exception);
            }
        }

        public void Dispose()
        {
        }
    }
}
