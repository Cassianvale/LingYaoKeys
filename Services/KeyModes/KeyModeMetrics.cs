using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

// 按键模式性能debug记录
namespace WpfApp.Services.KeyModes
{
    public class KeyModeMetrics
    {
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private readonly Queue<TimeSpan> _keyPressDurations;
        private readonly Queue<TimeSpan> _keyIntervals;
        private readonly object _metricsLock;
        private readonly Stopwatch _sequenceStopwatch;
        private int _totalKeyPresses;
        private const int MAX_TIMING_SAMPLES = 100;
        private int _keyCount = 0;
        

        public KeyModeMetrics()
        {
            _keyPressDurations = new Queue<TimeSpan>();
            _keyIntervals = new Queue<TimeSpan>();
            _metricsLock = new object();
            _sequenceStopwatch = new Stopwatch();
            _totalKeyPresses = 0;
        }

        public void StartTracking()
        {
            lock (_metricsLock)
            {
                _keyPressDurations.Clear();
                _keyIntervals.Clear();
                _totalKeyPresses = 0;
                _sequenceStopwatch.Restart();
            }
        }

        public void StopTracking()
        {
            _sequenceStopwatch.Stop();
        }

        public void RecordKeyPressDuration(TimeSpan duration)
        {
            if (duration.TotalMilliseconds <= 0) return;

            lock (_metricsLock)
            {
                _keyPressDurations.Enqueue(duration);
                while (_keyPressDurations.Count > MAX_TIMING_SAMPLES)
                {
                    _keyPressDurations.Dequeue();
                }
                _totalKeyPresses++;
            }
        }

        public void RecordKeyInterval(TimeSpan interval)
        {
            if (interval.TotalMilliseconds <= 0) return;
            
            lock (_metricsLock)
            {
                _keyIntervals.Enqueue(interval);
                while (_keyIntervals.Count > MAX_TIMING_SAMPLES)
                {
                    _keyIntervals.Dequeue();
                }
            }
        }

        public void IncrementKeyCount()
        {
            Interlocked.Increment(ref _keyCount);
        }
    }
} 