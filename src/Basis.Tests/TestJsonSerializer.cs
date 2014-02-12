using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Basis.Tests
{
    [TestFixture]
    public class TestJsonSerializer
    {
        [Test]
        public void CanCompressData()
        {
            var results =
                new[]
                {
                    1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024,
                    2048, 4096, 8192, 16384
                }
                    .Select(n => n*1024)
                    .Select(numChars =>
                    {
                        var serializer = new JsonSerializer();
                        var payload = new string(GetCharsOf("This is just a random string that will be repeated", numChars).ToArray());
                        var bytes = serializer.Serialize(new ObjectWithArbitrarySize {Payload = payload});

                        var clone = (ObjectWithArbitrarySize) serializer.Deserialize(bytes);
                        Assert.That(clone.Payload, Is.EqualTo(payload));

                        return new
                        {
                            PayloadLength = payload.Length,
                            BytesLength = bytes.Length
                        };
                    })
                    .ToList();

            Console.WriteLine(string.Join(Environment.NewLine,
                results.Select(r => string.Format("{0} => {1} bytes - factor {2:0.00}", r.PayloadLength, r.BytesLength, r.BytesLength/(double)r.PayloadLength))));
        }

        IEnumerable<char> GetCharsOf(string pattern, int numChars)
        {
            for(var index = 0; index < numChars; index++)
            {
                yield return pattern[index%pattern.Length];
            }
        }

        class ObjectWithArbitrarySize
        {
            public string Payload { get; set; }
        }
    }
}