using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WpfApp.Services.Models;

namespace WpfApp.Services
{
    public class RapidFireService : IDisposable
    {
        private readonly LyKeysService _lyKeysService;
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private readonly ConcurrentDictionary<LyKeysCode, CancellationTokenSource> _activeKeys;
        private readonly ConcurrentDictionary<LyKeysCode, KeyBurstConfig> _keyConfigs;
        private bool _isEnabled;
        private bool _isDisposed;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    if (!value)
                    {
                        StopAllKeys();
                    }
                }
            }
        }

        public RapidFireService(LyKeysService lyKeysService)
        {
            _lyKeysService = lyKeysService ?? throw new ArgumentNullException(nameof(lyKeysService));
            _activeKeys = new ConcurrentDictionary<LyKeysCode, CancellationTokenSource>();
            _keyConfigs = new ConcurrentDictionary<LyKeysCode, KeyBurstConfig>();
        }

        public void UpdateKeyConfig(KeyBurstConfig config)
        {
            if (config == null) return;

            _keyConfigs.AddOrUpdate(config.Code, config, (_, _) => config);
            _logger.Debug($"更新连发配置 - 按键: {config.Code}, 延迟: {config.RapidFireDelay}ms, 按压时长: {config.PressTime}ms");
        }

        public void UpdateKeyConfigs(IEnumerable<KeyBurstConfig> configs)
        {
            if (configs == null) return;

            _keyConfigs.Clear();
            foreach (var config in configs)
            {
                _keyConfigs.TryAdd(config.Code, config);
            }
            _logger.Debug($"批量更新连发配置完成");
        }

        public void StartKey(LyKeysCode keyCode)
        {
            if (!IsEnabled || !_keyConfigs.TryGetValue(keyCode, out var config))
            {
                return;
            }

            try
            {
                // 如果按键已经在运行，先停止它
                StopKey(keyCode);

                var cts = new CancellationTokenSource();
                if (_activeKeys.TryAdd(keyCode, cts))
                {
                    _ = RunKeyBurstAsync(keyCode, config, cts.Token);
                    _logger.Debug($"开始连发 - 按键: {keyCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"启动连发失败 - 按键: {keyCode}", ex);
            }
        }

        public void StopKey(LyKeysCode keyCode)
        {
            try
            {
                if (_activeKeys.TryRemove(keyCode, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                    _logger.Debug($"停止连发 - 按键: {keyCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"停止连发失败 - 按键: {keyCode}", ex);
            }
        }

        private void StopAllKeys()
        {
            foreach (var keyCode in _activeKeys.Keys)
            {
                StopKey(keyCode);
            }
            _logger.Debug("停止所有连发按键");
        }

        private async Task RunKeyBurstAsync(LyKeysCode keyCode, KeyBurstConfig config, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // 设置标记，表示这是模拟按键
                    _lyKeysService.IsSimulatedInput = true;
                    try
                    {
                        // 按下按键
                        _lyKeysService.SendKeyDown(keyCode);
                        
                        // 等待按压时长
                        await Task.Delay(config.PressTime, cancellationToken);
                        
                        // 释放按键
                        _lyKeysService.SendKeyUp(keyCode);
                    }
                    finally
                    {
                        // 恢复标记
                        _lyKeysService.IsSimulatedInput = false;
                    }
                    
                    // 等待连发间隔
                    await Task.Delay(config.RapidFireDelay, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，确保按键释放
                _lyKeysService.IsSimulatedInput = true;
                try
                {
                    _lyKeysService.SendKeyUp(keyCode);
                }
                finally
                {
                    _lyKeysService.IsSimulatedInput = false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"连发循环异常 - 按键: {keyCode}", ex);
                // 确保按键释放
                _lyKeysService.IsSimulatedInput = true;
                try
                {
                    _lyKeysService.SendKeyUp(keyCode);
                }
                finally
                {
                    _lyKeysService.IsSimulatedInput = false;
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            try
            {
                StopAllKeys();
                foreach (var cts in _activeKeys.Values)
                {
                    cts.Dispose();
                }
                _activeKeys.Clear();
                _keyConfigs.Clear();
            }
            catch (Exception ex)
            {
                _logger.Error("释放RapidFireService时发生异常", ex);
            }

            _isDisposed = true;
        }

        ~RapidFireService()
        {
            Dispose();
        }
    }
} 