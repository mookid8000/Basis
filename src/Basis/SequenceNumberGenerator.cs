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
            var seqNo = Interlocked.Increment(ref _sequenceNumber);

            return seqNo;
        }

        public void StartWith(long sequenceNumber)
        {
            _sequenceNumber = sequenceNumber;
        }
    }
}