using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Basis.Tests
{
    public abstract class FixtureBase
    {
        readonly List<IDisposable> _trackedDisposables = new List<IDisposable>();

        [SetUp]
        public void SetUp()
        {
            _trackedDisposables.Clear();

            DoSetUp();
        }

        protected virtual void DoSetUp()
        {
        }

        [TearDown]
        public void TearDown()
        {
            DoTearDown();

            _trackedDisposables.ForEach(d =>
            {
                Console.WriteLine("Disposing {0}", d);
                try
                {
                    d.Dispose();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                }
            });
        
            _trackedDisposables.Clear();
        }

        protected virtual void DoTearDown()
        {
        }

        protected TDisposable Track<TDisposable>(TDisposable disposableToTrack) where TDisposable : IDisposable
        {
            _trackedDisposables.Add(disposableToTrack);
            
            return disposableToTrack;
        }
    }
}