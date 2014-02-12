using System;
using System.Runtime.Serialization;
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
            try
            {
                var stringifiedObject = JsonConvert.SerializeObject(obj, Settings);

                return DefaultEncoding.GetBytes(stringifiedObject);
            }
            catch (Exception exception)
            {
                throw new SerializationException(string.Format("An error ocurred while attempting to serialize {0} to {1}-encoded JSON",
                    obj, DefaultEncoding), exception);
            }
        }

        public object Deserialize(byte[] bytes)
        {
            try
            {
                var stringifiedObject = DefaultEncoding.GetString(bytes);

                return JsonConvert.DeserializeObject(stringifiedObject, Settings);
            }
            catch (Exception exception)
            {
                throw new SerializationException(string.Format("An error ocurred while attemptin to deserialize {0} as {1}-encoded JSON",
                    GetStringRepresentationSafe(bytes), DefaultEncoding), exception);
            }
        }

        string GetStringRepresentationSafe(byte[] bytes)
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