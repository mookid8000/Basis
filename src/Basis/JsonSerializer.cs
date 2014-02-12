using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

        const byte NotValidFirstByteOfUtf8EncodedJson = (byte) '!';

        const int CompressionThreshold = 2048;

        public byte[] Serialize(object obj)
        {
            try
            {
                var stringifiedObject = JsonConvert.SerializeObject(obj, Settings);
                var bytes = DefaultEncoding.GetBytes(stringifiedObject);

                if (bytes.Length > CompressionThreshold)
                {
                    bytes = Compress(bytes);
                }

                return bytes;
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
                var stringifiedObject = DefaultEncoding.GetString(PossiblyDecompress(bytes));

                return JsonConvert.DeserializeObject(stringifiedObject, Settings);
            }
            catch (Exception exception)
            {
                throw new SerializationException(string.Format("An error ocurred while attemptin to deserialize {0} as {1}-encoded JSON",
                    GetStringRepresentationSafe(bytes), DefaultEncoding), exception);
            }
        }

        public byte[] Compress(byte[] input)
        {
            using (var output = new MemoryStream())
            {
                using (var zip = new GZipStream(output, CompressionMode.Compress))
                {
                    zip.Write(input, 0, input.Length);
                }

                var list = output.ToArray().ToList();
                list.Insert(0, NotValidFirstByteOfUtf8EncodedJson);
                return list.ToArray();
            }
        }

        public byte[] PossiblyDecompress(byte[] input)
        {
            if (input.Length == 0) return input;
            if (input[0] != NotValidFirstByteOfUtf8EncodedJson) return input;

            input = input.Skip(1).ToArray();

            using (var inputStream = new MemoryStream(input))
            {
                using (var zip = new GZipStream(inputStream, CompressionMode.Decompress))
                {
                    var bytes = new List<byte>();
                    var b = zip.ReadByte();
                    while (b != -1)
                    {
                        bytes.Add((byte)b);
                        b = zip.ReadByte();
                    }
                    return bytes.ToArray();
                }
            }
        }

        string GetStringRepresentationSafe(byte[] bytes)
        {
            if (bytes.Length > 0 && bytes[0] == NotValidFirstByteOfUtf8EncodedJson)
            {
                return string.Format("Compressed bytes ({0})", bytes.Length);
            }

            try
            {
                return DefaultEncoding.GetString(bytes);
            }
            catch (Exception exception)
            {
                return string.Format("Could not generate string out of byte[{0}]: {1}", bytes.Length, exception);
            }
        }
    }
}