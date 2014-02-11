using System.Threading.Tasks;

namespace Basis
{
    public interface IStreamHandler
    {
        long GetLastSequenceNumber();
        Task ProcessEvent(DeserializedEvent deserializedEvent);
    }
}