using System;
using System.Collections.Generic;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;
using Timer = System.Timers.Timer;

namespace Basis.Tests
{
    public abstract class FixtureBase
    {
        protected static readonly Logger Log = LogManager.GetCurrentClassLogger();
        readonly List<IDisposable> _trackedDisposables = new List<IDisposable>();

        [SetUp]
        public void SetUp()
        {
            LogManager.Configuration = new LoggingConfiguration
            {
                LoggingRules = { new LoggingRule("*", LogLevel.Info, new ConsoleTarget()) }
            };

            _trackedDisposables.Clear();

            DoSetUp();
        }

        protected virtual void DoSetUp()
        {
        }

        public IDisposable Every5s(Action action)
        {
            return new TimerCallbacker(TimeSpan.FromSeconds(5), action);
        }

        class TimerCallbacker : IDisposable
        {
            readonly Timer _timer = new Timer();

            public TimerCallbacker(TimeSpan interval, Action action)
            {
                _timer.Interval = interval.TotalMilliseconds;
                _timer.Elapsed += delegate
                {
                    try
                    {
                        action();
                    }
                    catch (Exception exception)
                    {
                        Log.Warn("Error during callback: {0}", exception);
                    }
                };
                _timer.Start();
            }

            public void Dispose()
            {
                _timer.Stop();
                _timer.Dispose();
            }
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