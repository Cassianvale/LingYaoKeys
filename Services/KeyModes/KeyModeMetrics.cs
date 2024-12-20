using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace WpfApp.Services.KeyModes
{
    public class KeyModeMetrics
    {
        private readonly Queue<TimeSpan> _keyPressDurations;
        private readonly Queue<TimeSpan> _keyIntervals;
        private readonly object _metricsLock;
        private readonly Stopwatch _sequenceStopwatch;
        private int _totalKeyPresses;
        private const int MAX_TIMING_SAMPLES = 100;
        private readonly LogManager _logger;
        private int _keyCount = 0;
        

        public KeyModeMetrics()
        {
            _keyPressDurations = new Queue<TimeSpan>();
            _keyIntervals = new Queue<TimeSpan>();
            _metricsLock = new object();
            _sequenceStopwatch = new Stopwatch();
            _totalKeyPresses = 0;
            _logger = LogManager.Instance;
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

        public PerformanceMetrics GetCurrentMetrics(IEnumerable<DDKeyCode> currentSequence)
        {
            lock (_metricsLock)
            {
                return new PerformanceMetrics
                {
                    AverageKeyPressTime = CalculateAverage(_keyPressDurations),
                    AverageKeyInterval = CalculateAverage(_keyIntervals),
                    TotalExecutionTime = _sequenceStopwatch.Elapsed,
                    TotalKeyPresses = _totalKeyPresses,
                    CurrentSequence = string.Join(", ", currentSequence)
                };
            }
        }

        private double CalculateAverage(Queue<TimeSpan> timings)
        {
            if (timings == null || timings.Count == 0)
                return 0;

            lock (_metricsLock)
            {
                double sum = 0;
                int count = 0;
                foreach (var timing in timings)
                {
                    if (timing.TotalMilliseconds > 0)
                    {
                        sum += timing.TotalMilliseconds;
                        count++;
                    }
                }
                return count > 0 ? sum / count : 0;
            }
        }

        public void LogSequenceEnd(int keyInterval)
        {
            double avgPressDuration;
            double avgInterval;

            lock (_metricsLock)
            {
                avgPressDuration = CalculateAverage(_keyPressDurations);
                avgInterval = CalculateAverage(_keyIntervals);
            }

            var details = $"\n├─ 执行时间: {_sequenceStopwatch.Elapsed.TotalSeconds:F2}s\n" +
                         $"├─ 总按键次数: {_totalKeyPresses}\n" +
                         $"├─ 平均按压时长: {avgPressDuration:F2}ms\n" +
                         $"├─ 平均实际间隔: {avgInterval:F2}ms\n" +
                         $"└─ 设定间隔: {keyInterval}ms";
            
            _logger.LogSequenceEvent("结束", details);
        }

        public void IncrementKeyCount()
        {
            Interlocked.Increment(ref _keyCount);
        }
    }
} 