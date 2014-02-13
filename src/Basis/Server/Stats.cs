namespace Basis.Server
{
    public class Stats
    {
        readonly Counter _savedEventBatchesCounter = new Counter();
        readonly Counter _specificEventsCounter = new Counter();
        readonly Counter _eventsPlayedBackCounter = new Counter();

        public void EventBatchSaved()
        {
            _savedEventBatchesCounter.Inc();
        }

        public Counter.Result GetSavedEventBatches()
        {
            return _savedEventBatchesCounter.GetResult();
        }
        public Counter.Result GetPlayedBackEvents()
        {
            return _eventsPlayedBackCounter.GetResult();
        }
        public Counter.Result GetSpecificallyRequestedEvents()
        {
            return _specificEventsCounter.GetResult();
        }

        public void Reset()
        {
            _savedEventBatchesCounter.Reset();
        }

        public void SpecificEventsReturned(int count)
        {
            _specificEventsCounter.Inc(count);
        }

        public void EventsPlayedBack(int count)
        {
            _eventsPlayedBackCounter.Inc(count);
        }
    }
}