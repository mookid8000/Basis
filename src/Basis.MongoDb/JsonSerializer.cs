using System;
using System.Text;
using Newtonsoft.Json;

namespace Basis.MongoDb
{
    public class JsonSerializer
    {
        static readonly Encoding DefaultEncoding = Encoding.UTF8;
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        };

        public byte[] Serialize(object obj)
        {
            return DefaultEncoding.GetBytes(JsonConvert.SerializeObject(obj, Settings));
        }

        public object Deserialize(byte[] bytes)
        {
            return JsonConvert.DeserializeObject(DefaultEncoding.GetString(bytes), Settings);
        }

        public string GetStringRepresentationSafe(byte[] bytes)
        {
            try
            {
                return DefaultEncoding.GetString(bytes);
            }
            catch(Exception exception)
            {
                return string.Format("Could not generate string out of byte[{0}]: {1}", bytes.Length, exception);
            }
        }
    }
}