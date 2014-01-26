using System.Threading;

namespace Basis.MongoDb
{
    public class SequenceNumberGenerator
    {
        long _sequenceNumber;
        public SequenceNumberGenerator(long initializationValue)
        {
            _sequenceNumber = initializationValue;
        }

        public long GetNextSequenceNumber()
        {
            return Interlocked.Increment(ref _sequenceNumber);
        }
    }
}