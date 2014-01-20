using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;

namespace Basis.MongoDb
{
    public class EventStoreClient : IDisposable
    {
        readonly string _eventStoreListenUri;
        HubConnection _hubConnection;
        IHubProxy _eventStoreProxy;

        public EventStoreClient(string eventStoreListenUri)
        {
            _eventStoreListenUri = eventStoreListenUri;
        }

        public void Start()
        {
            _hubConnection = new HubConnection(_eventStoreListenUri);
            _eventStoreProxy = _hubConnection.CreateHubProxy("EventStoreHub");
            _hubConnection.Start().Wait();
        }

        public async Task Save(IEnumerable<object> events)
        {
            await _eventStoreProxy.Invoke("Save", new EventBatchDto {Events = events.ToArray()});
        }

        public void Dispose()
        {
            if (_hubConnection != null)
            {
                _hubConnection.Dispose();
                _hubConnection = null;
            }
        }
    }

    public class EventBatchDto
    {
        public object[] Events { get; set; }
    }
}
