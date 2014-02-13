using System;
using System.Threading;

namespace Basis.Server
{
    public class Counter
    {
        long _count;
        DateTime _timeOfLastReset;

        public Counter()
        {
            Reset();
        }

        public void Reset()
        {
            lock (this)
            {
                _timeOfLastReset = DateTime.UtcNow;
            }
        }

        public void Inc()
        {
            Interlocked.Increment(ref _count);
        }
        
        public void Inc(int count)
        {
            Interlocked.Add(ref _count, count);
        }

        public Result GetResult()
        {
            lock (this)
            {
                var countToReturn = Interlocked.Exchange(ref _count, 0);
                var time = DateTime.UtcNow;
                var elapsed = time - _timeOfLastReset;
                _timeOfLastReset = time;
                return new Result(countToReturn, elapsed);
            }
        }

        public class Result
        {
            public Result(long count, TimeSpan elapsed)
            {
                Count = count;
                Elapsed = elapsed;
            }

            public long Count { get; private set; }
            public TimeSpan Elapsed { get; private set; }
        }
    }
}