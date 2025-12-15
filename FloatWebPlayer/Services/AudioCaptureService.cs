using System;
using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace FloatWebPlayer.Services
{
    /// <summary>
    /// 音频捕获服务
    /// 使用 WASAPI Loopback 捕获系统音频，重采样为 16kHz 单声道（Vosk 要求）
    /// </summary>
    public class AudioCaptureService : IDisposable
    {
        #region Constants

        /// <summary>
        /// Vosk 要求的采样率
        /// </summary>
        public const int TargetSampleRate = 16000;

        /// <summary>
        /// Vosk 要求的声道数
        /// </summary>
        public const int TargetChannels = 1;

        /// <summary>
        /// Vosk 要求的位深度
        /// </summary>
        public const int TargetBitsPerSample = 16;

        #endregion

        #region Singleton

        private static AudioCaptureService? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static AudioCaptureService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new AudioCaptureService();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// 音频数据可用事件
        /// 提供重采样后的 16kHz 单声道 PCM 数据
        /// </summary>
        public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

        /// <summary>
        /// 捕获状态变化事件
        /// </summary>
        public event EventHandler<CaptureState>? StateChanged;

        #endregion

        #region Fields

        private WasapiLoopbackCapture? _capture;
        private WaveFormat? _sourceFormat;
        private bool _disposed;
        private CaptureState _state = CaptureState.Stopped;
        private bool _isPaused;

        // 重采样相关
        private MediaFoundationResampler? _resampler;
        private BufferedWaveProvider? _bufferedProvider;

        #endregion

        #region Properties

        /// <summary>
        /// 当前捕获状态
        /// </summary>
        public CaptureState State => _state;

        /// <summary>
        /// 是否正在捕获
        /// </summary>
        public bool IsCapturing => _state == CaptureState.Capturing;

        /// <summary>
        /// 是否已暂停
        /// </summary>
        public bool IsPaused => _isPaused;

        /// <summary>
        /// 目标音频格式（16kHz 单声道 16bit）
        /// </summary>
        public WaveFormat TargetFormat { get; } = new WaveFormat(TargetSampleRate, TargetBitsPerSample, TargetChannels);

        #endregion

        #region Constructor

        private AudioCaptureService()
        {
            // 私有构造函数，单例模式
        }

        /// <summary>
        /// 用于测试的内部构造函数
        /// </summary>
        internal AudioCaptureService(bool forTesting)
        {
            // 测试用构造函数
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 开始捕获系统音频
        /// </summary>
        public void Start()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioCaptureService));

            if (_state == CaptureState.Capturing)
                return;

            try
            {
                InitializeCapture();
                _capture?.StartRecording();
                _isPaused = false;
                SetState(CaptureState.Capturing);
                Log("音频捕获已启动");
            }
            catch (Exception ex)
            {
                Log($"启动音频捕获失败: {ex.Message}");
                SetState(CaptureState.Stopped);
                throw;
            }
        }

        /// <summary>
        /// 停止捕获
        /// </summary>
        public void Stop()
        {
            if (_state == CaptureState.Stopped)
                return;

            try
            {
                _capture?.StopRecording();
                CleanupCapture();
                _isPaused = false;
                SetState(CaptureState.Stopped);
                Log("音频捕获已停止");
            }
            catch (Exception ex)
            {
                Log($"停止音频捕获失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 暂停捕获（继续捕获但不触发事件）
        /// </summary>
        public void Pause()
        {
            if (_state != CaptureState.Capturing)
                return;

            _isPaused = true;
            Log("音频捕获已暂停");
        }

        /// <summary>
        /// 恢复捕获
        /// </summary>
        public void Resume()
        {
            if (_state != CaptureState.Capturing)
                return;

            _isPaused = false;
            Log("音频捕获已恢复");
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 初始化捕获设备
        /// </summary>
        private void InitializeCapture()
        {
            CleanupCapture();

            // 创建 WASAPI Loopback 捕获
            _capture = new WasapiLoopbackCapture();
            _sourceFormat = _capture.WaveFormat;

            Log($"源音频格式: {_sourceFormat.SampleRate}Hz, {_sourceFormat.Channels}ch, {_sourceFormat.BitsPerSample}bit");

            // 创建缓冲提供器用于重采样
            _bufferedProvider = new BufferedWaveProvider(_sourceFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(5),
                DiscardOnBufferOverflow = true
            };

            // 创建重采样器
            _resampler = new MediaFoundationResampler(_bufferedProvider, TargetFormat)
            {
                ResamplerQuality = 60 // 高质量重采样
            };

            // 订阅数据可用事件
            _capture.DataAvailable += OnCaptureDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
        }

        /// <summary>
        /// 清理捕获资源
        /// </summary>
        private void CleanupCapture()
        {
            if (_capture != null)
            {
                _capture.DataAvailable -= OnCaptureDataAvailable;
                _capture.RecordingStopped -= OnRecordingStopped;
                _capture.Dispose();
                _capture = null;
            }

            _resampler?.Dispose();
            _resampler = null;

            _bufferedProvider = null;
            _sourceFormat = null;
        }

        /// <summary>
        /// 捕获数据可用回调
        /// </summary>
        private void OnCaptureDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_isPaused || e.BytesRecorded == 0)
                return;

            try
            {
                // 将数据添加到缓冲区
                _bufferedProvider?.AddSamples(e.Buffer, 0, e.BytesRecorded);

                // 从重采样器读取数据
                if (_resampler != null)
                {
                    var buffer = new byte[e.BytesRecorded];
                    int bytesRead;

                    while ((bytesRead = _resampler.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        // 创建正确大小的数组
                        var outputBuffer = new byte[bytesRead];
                        Array.Copy(buffer, outputBuffer, bytesRead);

                        // 触发事件
                        AudioDataAvailable?.Invoke(this, new AudioDataEventArgs(outputBuffer, bytesRead));
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"处理音频数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 录制停止回调
        /// </summary>
        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                Log($"录制异常停止: {e.Exception.Message}");
            }

            SetState(CaptureState.Stopped);
        }

        /// <summary>
        /// 设置状态并触发事件
        /// </summary>
        private void SetState(CaptureState newState)
        {
            if (_state != newState)
            {
                _state = newState;
                StateChanged?.Invoke(this, newState);
            }
        }

        /// <summary>
        /// 记录日志
        /// </summary>
        private void Log(string message)
        {
            Debug.WriteLine($"[AudioCaptureService] {message}");
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                Stop();
                CleanupCapture();
            }

            _disposed = true;
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~AudioCaptureService()
        {
            Dispose(false);
        }

        #endregion
    }

    /// <summary>
    /// 捕获状态枚举
    /// </summary>
    public enum CaptureState
    {
        /// <summary>
        /// 已停止
        /// </summary>
        Stopped,

        /// <summary>
        /// 正在捕获
        /// </summary>
        Capturing
    }

    /// <summary>
    /// 音频数据事件参数
    /// </summary>
    public class AudioDataEventArgs : EventArgs
    {
        /// <summary>
        /// 音频数据（16kHz 单声道 16bit PCM）
        /// </summary>
        public byte[] Buffer { get; }

        /// <summary>
        /// 有效字节数
        /// </summary>
        public int BytesRecorded { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public AudioDataEventArgs(byte[] buffer, int bytesRecorded)
        {
            Buffer = buffer;
            BytesRecorded = bytesRecorded;
        }
    }
}
