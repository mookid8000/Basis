namespace Basis.Messages
{
    public class RequestSpecificEventsArgs
    {
        public RequestSpecificEventsArgs(long[] eventNumbers)
        {
            EventNumbers = eventNumbers;
        }

        public long[] EventNumbers { get; protected set; }
    }
}