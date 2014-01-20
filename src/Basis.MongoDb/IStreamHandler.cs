using System.Collections.Generic;
using System.Threading.Tasks;

namespace Basis.MongoDb
{
    public interface IStreamHandler
    {
        long GetLastSequenceNumber();
        Task ProcessEvents(IEnumerable<object> events);
        Task Commit(long newLastSequenceNumber);
    }
}