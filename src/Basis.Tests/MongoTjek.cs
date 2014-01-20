using System;
using NUnit.Framework;

namespace Basis.Tests
{
    [TestFixture]
    public class MongoTjek : MongoFixture
    {
        [Test]
        public void CanQueryOplog()
        {
            var database = GetDatabase("local");

            var oplog = database.GetCollection("oplog.rs");

            Console.WriteLine(string.Join(Environment.NewLine, oplog.FindAll().SetLimit(20)));
        }
    }
}