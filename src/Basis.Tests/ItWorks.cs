﻿using System;
using System.Linq;
using System.Text;
using System.Threading;
using Basis.MongoDb;
using MongoDB.Driver;
using NUnit.Framework;

namespace Basis.Tests
{
    [TestFixture]
    public class ItWorks : FixtureBase
    {
        EventStream _eventStream;
        const string MongoDbConnectionString = "mongodb://localhost/basis_test";

        protected override void DoSetUp()
        {
            var database = new MongoClient(MongoDbConnectionString)
                .GetServer()
                .GetDatabase(new MongoUrl(MongoDbConnectionString).DatabaseName);

            _eventStream = new EventStream(database, "events");
        }

        [Test]
        public void Yay()
        {
            var greeting = new StringBuilder();

            _eventStream.Handle(batch =>
            {
                foreach (var evt in batch.OfType<SomeoneSaid>())
                {
                    if (greeting.Length == 0)
                    {
                        greeting.Append(evt.Value);
                    }
                    else
                    {
                        greeting.Append(" " + evt.Value);
                    }
                }
            });

            _eventStream.Start();

            _eventStream.Save(new[]
            {
                new SomeoneSaid {Value = "hello"},
                new SomeoneSaid {Value = "world"}
            });

            Thread.Sleep(TimeSpan.FromSeconds(1));

            Assert.That(greeting.ToString(), Is.EqualTo("hello world"));
        }

        class SomeoneSaid
        {
            public string Value { get; set; }
        }
    }
}
