using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WpfApp.Services;
using WpfApp.Services.Collections;


public class TaskManager
{
    private readonly ConcurrentDictionary<string, Task> _activeTasks = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _tokenSources = new();
    private readonly SemaphoreSlim _throttler;
    private readonly int _maxConcurrentTasks;
    private static readonly SerilogManager _logger = SerilogManager.Instance;
    private readonly ConcurrentPriorityQueue<string, Task> _priorityTasks = new();
    
    public TaskManager(int maxConcurrentTasks = 4)
    {
        _maxConcurrentTasks = maxConcurrentTasks;
        _throttler = new SemaphoreSlim(maxConcurrentTasks);
    }

    public async Task StartTask(string taskId, Func<CancellationToken, Task> work, TaskPriority priority = TaskPriority.Normal)
    {
        await StopTask(taskId, TimeSpan.FromSeconds(1));
        
        await _throttler.WaitAsync();
        try
        {
            var cts = new CancellationTokenSource();
            _tokenSources.TryAdd(taskId, cts);
            
            var task = Task.Factory.StartNew(async () =>
            {
                try
                {
                    if (priority == TaskPriority.High)
                    {
                        Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                    }
                    
                    await work(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // 正常取消
                }
                finally
                {
                    _throttler.Release();
                    CleanupTask(taskId);
                }
            }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default)
            .Unwrap();
            
            _priorityTasks.Add(taskId, task, (int)priority);
        }
        catch
        {
            _throttler.Release();
            throw;
        }
    }

    private void CleanupTask(string taskId)
    {
        _activeTasks.TryRemove(taskId, out _);
        if (_tokenSources.TryRemove(taskId, out var token))
        {
            token.Dispose();
        }
        _priorityTasks.TryRemove(taskId);
    }

    public async Task StopTask(string taskId, TimeSpan timeout)
    {
        if (_tokenSources.TryRemove(taskId, out var cts))
        {
            try
            {
                cts.Cancel();
                if (_activeTasks.TryGetValue(taskId, out var task))
                {
                    var timeoutTask = Task.Delay(timeout);
                    var completedTask = await Task.WhenAny(task, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        // 任务超时，强制终止
                        _activeTasks.TryRemove(taskId, out _);
                    }
                }
            }
            finally
            {
                cts.Dispose();
            }
        }
    }

    public async Task StopAllTasks(TimeSpan timeout)
    {
        var tasks = _activeTasks.Keys.ToList();
        await Task.WhenAll(tasks.Select(id => StopTask(id, timeout)));
    }

    public bool IsTaskRunning(string taskId)
    {
        return _activeTasks.ContainsKey(taskId);
    }
}

public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2
} 