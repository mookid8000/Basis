using System.Collections.Generic;

namespace Basis.Messages
{
    public class EventBatchToSave
    {
        public List<EventToSave> Events { get; set; }
    }

    public class EventToSave
    {
        public Dictionary<string,string> Meta { get; set; }
        public byte[] Body { get; set; }
    }
}