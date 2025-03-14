using System.Collections.Concurrent;

namespace WpfApp.Services.Utils;

public class TaskManager
{
    private readonly ConcurrentDictionary<string, Task> _activeTasks = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _tokenSources = new();
    private readonly SemaphoreSlim _throttler;
    private readonly int _maxConcurrentTasks;

    public TaskManager(int maxConcurrentTasks = 4)
    {
        _maxConcurrentTasks = maxConcurrentTasks;
        _throttler = new SemaphoreSlim(maxConcurrentTasks);
    }

    // 启动任务
    public async Task StartTask(string taskId, Func<CancellationToken, Task> work)
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

            _activeTasks.TryAdd(taskId, task);
        }
        catch
        {
            _throttler.Release();
            throw;
        }
    }

    // 清理任务
    private void CleanupTask(string taskId)
    {
        _activeTasks.TryRemove(taskId, out _);
        if (_tokenSources.TryRemove(taskId, out var token)) token.Dispose();
    }

    // 停止任务
    public async Task StopTask(string taskId, TimeSpan timeout)
    {
        if (_tokenSources.TryRemove(taskId, out var cts))
            try
            {
                cts.Cancel();
                if (_activeTasks.TryGetValue(taskId, out var task))
                {
                    var timeoutTask = Task.Delay(timeout);
                    var completedTask = await Task.WhenAny(task, timeoutTask);
                    if (completedTask == timeoutTask)
                        // 任务超时，强制终止
                        _activeTasks.TryRemove(taskId, out _);
                }
            }
            finally
            {
                cts.Dispose();
            }
    }

    // 停止所有任务
    public async Task StopAllTasks(TimeSpan timeout)
    {
        var tasks = _activeTasks.Keys.ToList();
        await Task.WhenAll(tasks.Select(id => StopTask(id, timeout)));
    }

    // 检查任务是否正在运行
    public bool IsTaskRunning(string taskId)
    {
        return _activeTasks.ContainsKey(taskId);
    }
}