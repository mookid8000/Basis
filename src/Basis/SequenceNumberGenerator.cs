using System;
using System.Threading;

namespace Basis
{
    public class SequenceNumberGenerator
    {
        readonly object _lockObject = new object();
        long _sequenceNumber;
        
        public Generator GetGenerator()
        {
            return new Generator(_lockObject, this);
        }

        public void StartWith(long sequenceNumber)
        {
            _sequenceNumber = sequenceNumber;
        }

        public class Generator : IDisposable
        {
            readonly object _lockObject;
            readonly SequenceNumberGenerator _numberGenerator;

            public Generator(object lockObject, SequenceNumberGenerator numberGenerator)
            {
                _lockObject = lockObject;
                _numberGenerator = numberGenerator;
                Monitor.Enter(_lockObject);
            }

            public void Dispose()
            {
                Monitor.Exit(_lockObject);
            }

            public long GetNextSequenceNumber()
            {
                return _numberGenerator.GetNextSequenceNumber();
            }
        }

        long GetNextSequenceNumber()
        {
            var seqNo = Interlocked.Increment(ref _sequenceNumber);

            return seqNo;
        }
    }
}