using System;
using System.Text;
using Newtonsoft.Json;

namespace Basis
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
            var stringifiedObject = JsonConvert.SerializeObject(obj, Settings);

            return DefaultEncoding.GetBytes(stringifiedObject);
        }

        public object Deserialize(byte[] bytes)
        {
            var stringifiedObject = DefaultEncoding.GetString(bytes);

            return JsonConvert.DeserializeObject(stringifiedObject, Settings);
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