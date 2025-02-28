using System.IO;
using System.Reflection;
using NAudio.Wave;
using WpfApp.Services.Utils;

namespace WpfApp.Services
{
    public class AudioService
    {
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private readonly string _startSoundPath;
        private readonly string _stopSoundPath;
        private WaveOutEvent _outputDevice;
        private MediaFoundationReader _mediaReader;
        private readonly object _lockObject = new object();
        private CancellationTokenSource _currentCts;
        private bool _isPlayingStopSound;
        private bool _isDisposed;
        private readonly object _disposeLock = new object();
        
        public bool IsDisposed => _isDisposed;

        public AudioService()
        {
            string userDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".lykeys",
                "sound"
            );
            
            try
            {
                Directory.CreateDirectory(userDataPath);
                _logger.Debug($"创建音频文件目录: {userDataPath}");

                _startSoundPath = Path.Combine(userDataPath, "start.mp3");
                _stopSoundPath = Path.Combine(userDataPath, "stop.mp3");

                // 确保音频文件存在
                EnsureAudioFileExists("start.mp3", _startSoundPath);
                EnsureAudioFileExists("stop.mp3", _stopSoundPath);
                
                _logger.Debug($"音频文件初始化完成:");
                _logger.Debug($"- 开始音效: {_startSoundPath}");
                _logger.Debug($"- 结束音效: {_stopSoundPath}");

                // 验证音频文件
                if (!File.Exists(_startSoundPath) || !File.Exists(_stopSoundPath))
                {
                    _logger.Error("音频文件初始化失败，文件不存在");
                    throw new FileNotFoundException("音频文件初始化失败，文件不存在");
                }

                // 验证音频文件可访问性
                using (var testReader = new MediaFoundationReader(_startSoundPath))
                using (var testDevice = new WaveOutEvent())
                {
                    testDevice.Init(testReader);
                    _logger.Debug("音频设备初始化测试成功");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("音频服务初始化失败", ex);
                throw;
            }
        }

        private void EnsureAudioFileExists(string fileName, string targetPath)
        {
            try
            {
                if (!File.Exists(targetPath))
                {
                    _logger.Debug($"开始提取音频文件: {fileName}");
                    string resourceName = $"WpfApp.Resource.sound.{fileName}";
                    
                    using (Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                    {
                        if (stream is null)
                        {
                            _logger.Error($"找不到音频资源：{resourceName}");
                            throw new FileNotFoundException($"找不到音频资源：{resourceName}");
                        }

                        using (FileStream fileStream = File.Create(targetPath))
                        {
                            stream.CopyTo(fileStream);
                            _logger.Debug($"音频文件提取成功: {targetPath}");
                        }
                    }
                }
                else
                {
                    _logger.Debug($"音频文件已存在: {targetPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"提取音频文件失败: {fileName}", ex);
                throw;
            }
        }

        public async Task PlayStartSound()
        {
            lock (_lockObject)
            {
                // 如果正在播放停止音效，立即停止
                if (_isPlayingStopSound)
                {
                    DisposeCurrentSound();
                }
            }
            await PlaySound(_startSoundPath, true);
        }

        public async Task PlayStopSound()
        {
            await PlaySound(_stopSoundPath, false);
        }

        private async Task PlaySound(string path, bool isStartSound)
        {
            if (!File.Exists(path))
            {
                _logger.Error($"音频文件不存在: {path}");
                return;
            }

            try
            {
                lock (_lockObject)
                {
                    // 取消之前的播放任务（如果有）
                    _currentCts?.Cancel();
                    _currentCts = new CancellationTokenSource();
                    
                    // 如果当前有音效在播放，停止它
                    if (_outputDevice != null)
                    {
                        DisposeCurrentSound();
                    }
                    
                    _isPlayingStopSound = !isStartSound;
                }

                var cts = _currentCts;

                // 创建新的播放实例
                var mediaReader = new MediaFoundationReader(path);
                var outputDevice = new WaveOutEvent();
                
                lock (_lockObject)
                {
                    if (cts.IsCancellationRequested)
                    {
                        mediaReader.Dispose();
                        outputDevice.Dispose();
                        return;
                    }
                    
                    _mediaReader = mediaReader;
                    _outputDevice = outputDevice;
                }

                var tcs = new TaskCompletionSource<bool>();
                outputDevice.PlaybackStopped += (s, e) =>
                {
                    tcs.TrySetResult(true);
                    lock (_lockObject)
                    {
                        if (_outputDevice == outputDevice)
                        {
                            _isPlayingStopSound = false;
                            DisposeCurrentSound();
                        }
                    }
                };

                outputDevice.Init(mediaReader);
                outputDevice.Play();

                try
                {
                    await tcs.Task.WaitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.Debug("音频播放被取消");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"播放声音失败: {path}", ex);
                lock (_lockObject)
                {
                    _isPlayingStopSound = false;
                    DisposeCurrentSound();
                }
            }
        }

        private void DisposeCurrentSound()
        {
            try
            {
                if (_outputDevice != null)
                {
                    _outputDevice.Stop();
                    _outputDevice.Dispose();
                    _outputDevice = null;
                }

                if (_mediaReader != null)
                {
                    _mediaReader.Dispose();
                    _mediaReader = null;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("释放当前音频资源时发生异常", ex);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            lock (_disposeLock)
            {
                if (_isDisposed) return;
                _isDisposed = true;

                try
                {
                    // 取消当前播放任务
                    if (_currentCts != null)
                    {
                        _currentCts.Cancel();
                        _currentCts.Dispose();
                        _currentCts = null;
                    }

                    // 停止并释放当前音频
                    lock (_lockObject)
                    {
                        _isPlayingStopSound = false;
                        DisposeCurrentSound();
                    }

                    _logger.Debug("音频服务资源已释放");
                }
                catch (Exception ex)
                {
                    _logger.Error("释放音频服务资源时发生异常", ex);
                }
            }
            
            GC.SuppressFinalize(this);
        }
    }
} 