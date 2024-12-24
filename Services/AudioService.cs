using NAudio.Wave;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Reflection;

namespace WpfApp.Services
{
    public class AudioService
    {
        private readonly LogManager _logger = LogManager.Instance;
        private readonly string _startSoundPath;
        private readonly string _stopSoundPath;
        private WaveOutEvent _outputDevice;
        private MediaFoundationReader _mediaReader;
        private readonly object _lockObject = new object();
        private CancellationTokenSource _currentCts;
        private bool _isPlayingStopSound;

        public AudioService()
        {
            string userDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".lingyao",
                "sound"
            );
            Directory.CreateDirectory(userDataPath);

            _startSoundPath = Path.Combine(userDataPath, "start.mp3");
            _stopSoundPath = Path.Combine(userDataPath, "stop.mp3");

            // 确保音频文件存在
            EnsureAudioFileExists("start.mp3", _startSoundPath);
            EnsureAudioFileExists("stop.mp3", _stopSoundPath);
        }

        private void EnsureAudioFileExists(string fileName, string targetPath)
        {
            if (!File.Exists(targetPath))
            {
                string resourceName = $"WpfApp.Resource.sound.{fileName}";
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        _logger.LogError("AudioService", $"找不到音频资源：{resourceName}");
                        return;
                    }

                    using (FileStream fileStream = File.Create(targetPath))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
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
                _logger.LogError("AudioService", $"音频文件不存在: {path}");
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
                    _logger.LogDebug("AudioService", "音频播放被取消");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("AudioService", $"播放声音失败: {path}", ex);
                lock (_lockObject)
                {
                    _isPlayingStopSound = false;
                    DisposeCurrentSound();
                }
            }
        }

        private void DisposeCurrentSound()
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

        public void Dispose()
        {
            _currentCts?.Cancel();
            _currentCts?.Dispose();
            lock (_lockObject)
            {
                _isPlayingStopSound = false;
                DisposeCurrentSound();
            }
        }
    }
} 