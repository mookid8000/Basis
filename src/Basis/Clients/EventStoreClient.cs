using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Basis.Messages;
using Basis.Server;
using Microsoft.AspNet.SignalR.Client;
using NLog;

namespace Basis.Clients
{
    public class EventStoreClient : IDisposable
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        readonly JsonSerializer _serializer = new JsonSerializer();
        readonly string _eventStoreListenUri;
        HubConnection _hubConnection;
        IHubProxy _eventStoreProxy;
        bool _started;

        public EventStoreClient(string eventStoreListenUri)
        {
            _eventStoreListenUri = eventStoreListenUri;
        }

        public void Start()
        {
            if (_started)
            {
                throw new InvalidOperationException("Event store client has already been started! Cannot start event store client twice!");
            }

            Log.Info("Starting event store client for {0}", _eventStoreListenUri);
            _hubConnection = new HubConnection(_eventStoreListenUri);
            _eventStoreProxy = _hubConnection.CreateHubProxy(typeof(EventStoreHub).Name);
            
            Log.Debug("Opening connection");
            _hubConnection.Start().Wait();

            Log.Info("Event store client started");
            _started = true;
        }

        public async Task Save(IEnumerable<object> events)
        {
            try
            {
                await _eventStoreProxy.Invoke("Save", new EventBatchToSave
                {
                    Events = events.Select(e => _serializer.Serialize(e)).ToArray()
                });
            }
            catch (Exception ex)
            {
                throw new ApplicationException(
                    string.Format("Exception occured while trying to store the following types of events: {0}",
                        string.Join(",", events.Select(t => t.GetType().Name))), ex);
            }
        }

        public void Dispose()
        {
            if (_hubConnection != null)
            {
                Log.Info("Shutting down event store client for {0}", _eventStoreListenUri);
                _hubConnection.Dispose();
                
                _hubConnection = null;
                Log.Info("Event store client stopped");
            }

            _started = false;
        }
    }
}
