using System.Threading.Tasks;

namespace Basis.MongoDb
{
    public interface IStreamHandler
    {
        long GetLastSequenceNumber();
        Task ProcessEvents(DeserializedEvent deserializedEvent);
    }
}