using System.Collections.Generic;

namespace Basis.MongoDb
{
    public interface IStreamHandler
    {
        long GetLastSequenceNumber();
        void ProcessEvents(IEnumerable<object> events);
        void Commit(long newLastSequenceNumber);
    }
}