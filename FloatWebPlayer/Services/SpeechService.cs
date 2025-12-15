using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FloatWebPlayer.Helpers;
using Vosk;

namespace FloatWebPlayer.Services
{
    /// <summary>
    /// 语音识别服务
    /// 使用 Vosk 进行离线语音识别
    /// </summary>
    public class SpeechService : IDisposable
    {
        #region Constants

        /// <summary>
        /// 模型目录名称
        /// </summary>
        private const string ModelDirectoryName = "vosk-model-small-cn-0.22";

        /// <summary>
        /// 模型 ZIP 文件名
        /// </summary>
        private const string ModelZipFileName = "vosk-model-small-cn-0.22.zip";

        /// <summary>
        /// 单源下载超时时间（秒）
        /// </summary>
        private const int SourceTimeoutSeconds = 30;

        #endregion

        #region Model Sources

        /// <summary>
        /// 模型下载源列表（按优先级排序）
        /// </summary>
        private static readonly (string Name, string Url)[] ModelSources = new[]
        {
            ("Alphacep", "https://alphacephei.com/vosk/models/vosk-model-small-cn-0.22.zip"),
            ("ModelScope", "https://modelscope.cn/models/Bovin12/vosk-model-small-cn-0.22/resolve/master/vosk-model-small-cn-0.22.zip")
        };

        #endregion

        #region Singleton

        private static SpeechService? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static SpeechService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SpeechService();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// 文本识别事件
        /// </summary>
        public event EventHandler<SpeechResult>? TextRecognized;

        /// <summary>
        /// 状态变化事件
        /// </summary>
        public event EventHandler<SpeechServiceState>? StateChanged;

        #endregion


        #region Fields

        private Model? _model;
        private VoskRecognizer? _recognizer;
        private bool _disposed;
        private SpeechServiceState _state = SpeechServiceState.Stopped;
        private readonly AudioCaptureService _audioCaptureService;

        #endregion

        #region Properties

        /// <summary>
        /// 模型目录路径
        /// </summary>
        public string ModelPath => Path.Combine(AppPaths.DataDirectory, "Models", ModelDirectoryName);

        /// <summary>
        /// 模型 ZIP 文件路径
        /// </summary>
        private string ModelZipPath => Path.Combine(AppPaths.DataDirectory, "Models", ModelZipFileName);

        /// <summary>
        /// 模型目录
        /// </summary>
        private string ModelsDirectory => Path.Combine(AppPaths.DataDirectory, "Models");

        /// <summary>
        /// 模型是否已安装
        /// </summary>
        public bool IsModelInstalled
        {
            get
            {
                // 检查模型目录是否存在且包含必要文件
                if (!Directory.Exists(ModelPath))
                    return false;

                // 检查是否存在 am 目录（Vosk 模型必需）
                var amPath = Path.Combine(ModelPath, "am");
                if (!Directory.Exists(amPath))
                    return false;

                // 检查是否存在 conf 目录
                var confPath = Path.Combine(ModelPath, "conf");
                if (!Directory.Exists(confPath))
                    return false;

                return true;
            }
        }

        /// <summary>
        /// 当前状态
        /// </summary>
        public SpeechServiceState State => _state;

        /// <summary>
        /// 是否正在识别
        /// </summary>
        public bool IsRecognizing => _state == SpeechServiceState.Recognizing;

        #endregion

        #region Constructor

        private SpeechService()
        {
            _audioCaptureService = AudioCaptureService.Instance;
        }

        /// <summary>
        /// 用于测试的内部构造函数
        /// </summary>
        internal SpeechService(AudioCaptureService audioCaptureService)
        {
            _audioCaptureService = audioCaptureService;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 启动语音识别
        /// </summary>
        public void Start()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SpeechService));

            if (_state == SpeechServiceState.Recognizing)
                return;

            if (!IsModelInstalled)
            {
                Log("语音模型未安装，无法启动识别");
                throw new InvalidOperationException("语音模型未安装，请先下载模型");
            }

            try
            {
                InitializeRecognizer();
                
                // 订阅音频数据事件
                _audioCaptureService.AudioDataAvailable += OnAudioDataAvailable;
                
                // 启动音频捕获
                _audioCaptureService.Start();
                
                SetState(SpeechServiceState.Recognizing);
                Log("语音识别已启动");
            }
            catch (Exception ex)
            {
                Log($"启动语音识别失败: {ex.Message}");
                CleanupRecognizer();
                SetState(SpeechServiceState.Stopped);
                throw;
            }
        }

        /// <summary>
        /// 停止语音识别
        /// </summary>
        public void Stop()
        {
            if (_state == SpeechServiceState.Stopped)
                return;

            try
            {
                // 取消订阅音频数据事件
                _audioCaptureService.AudioDataAvailable -= OnAudioDataAvailable;
                
                // 停止音频捕获
                _audioCaptureService.Stop();
                
                // 获取最终结果
                if (_recognizer != null)
                {
                    var finalResult = _recognizer.FinalResult();
                    ProcessRecognitionResult(finalResult, true);
                }
                
                CleanupRecognizer();
                SetState(SpeechServiceState.Stopped);
                Log("语音识别已停止");
            }
            catch (Exception ex)
            {
                Log($"停止语音识别失败: {ex.Message}");
            }
        }


        /// <summary>
        /// 下载语音模型
        /// </summary>
        /// <param name="progress">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task DownloadModelAsync(IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SpeechService));

            // 确保模型目录存在
            Directory.CreateDirectory(ModelsDirectory);

            // 如果模型已安装，直接返回
            if (IsModelInstalled)
            {
                Log("模型已安装，跳过下载");
                progress?.Report(new ModelDownloadProgress(100, "已安装", ModelDownloadStatus.Completed));
                return;
            }

            // 删除可能存在的不完整文件
            if (File.Exists(ModelZipPath))
            {
                File.Delete(ModelZipPath);
            }

            Exception? lastException = null;

            // 尝试从各个源下载
            foreach (var (sourceName, sourceUrl) in ModelSources)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                Log($"尝试从 {sourceName} 下载模型...");
                progress?.Report(new ModelDownloadProgress(0, sourceName, ModelDownloadStatus.Downloading));

                try
                {
                    await DownloadFromSourceAsync(sourceUrl, sourceName, progress, cancellationToken);
                    
                    // 下载成功，校验并解压
                    progress?.Report(new ModelDownloadProgress(100, sourceName, ModelDownloadStatus.Extracting));
                    
                    if (await ExtractModelAsync(progress, cancellationToken))
                    {
                        Log($"从 {sourceName} 下载并解压成功");
                        progress?.Report(new ModelDownloadProgress(100, sourceName, ModelDownloadStatus.Completed));
                        return;
                    }
                    else
                    {
                        Log($"从 {sourceName} 下载的文件解压失败");
                        lastException = new InvalidOperationException("ZIP 文件解压失败");
                    }
                }
                catch (OperationCanceledException)
                {
                    Log($"从 {sourceName} 下载被取消");
                    throw;
                }
                catch (Exception ex)
                {
                    Log($"从 {sourceName} 下载失败: {ex.Message}");
                    lastException = ex;
                    
                    // 清理可能的不完整文件
                    if (File.Exists(ModelZipPath))
                    {
                        try { File.Delete(ModelZipPath); } catch { }
                    }
                }
            }

            // 所有源都失败
            progress?.Report(new ModelDownloadProgress(0, "失败", ModelDownloadStatus.Failed));
            throw new InvalidOperationException("所有下载源均失败", lastException);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 从指定源下载模型
        /// </summary>
        private async Task DownloadFromSourceAsync(string url, string sourceName, IProgress<ModelDownloadProgress>? progress, CancellationToken cancellationToken)
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(SourceTimeoutSeconds);

            // 创建带超时的取消令牌
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(SourceTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var downloadedBytes = 0L;

            using var contentStream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
            using var fileStream = new FileStream(ModelZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int bytesRead;
            var lastProgressReport = DateTime.Now;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, linkedCts.Token)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, linkedCts.Token);
                downloadedBytes += bytesRead;

                // 每 100ms 报告一次进度
                if ((DateTime.Now - lastProgressReport).TotalMilliseconds >= 100)
                {
                    var percentage = totalBytes > 0 ? (int)(downloadedBytes * 100 / totalBytes) : -1;
                    progress?.Report(new ModelDownloadProgress(percentage, sourceName, ModelDownloadStatus.Downloading, downloadedBytes, totalBytes));
                    lastProgressReport = DateTime.Now;
                }
            }

            // 最终进度报告
            progress?.Report(new ModelDownloadProgress(100, sourceName, ModelDownloadStatus.Downloading, downloadedBytes, totalBytes));
        }


        /// <summary>
        /// 解压模型文件
        /// </summary>
        private async Task<bool> ExtractModelAsync(IProgress<ModelDownloadProgress>? progress, CancellationToken cancellationToken)
        {
            if (!File.Exists(ModelZipPath))
                return false;

            try
            {
                // 如果目标目录已存在，先删除
                if (Directory.Exists(ModelPath))
                {
                    Directory.Delete(ModelPath, true);
                }

                // 解压到模型目录
                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(ModelZipPath, ModelsDirectory);
                }, cancellationToken);

                // 删除 ZIP 文件
                File.Delete(ModelZipPath);

                // 验证解压结果
                return IsModelInstalled;
            }
            catch (Exception ex)
            {
                Log($"解压模型失败: {ex.Message}");
                
                // 清理可能的不完整目录
                if (Directory.Exists(ModelPath))
                {
                    try { Directory.Delete(ModelPath, true); } catch { }
                }
                
                return false;
            }
        }

        /// <summary>
        /// 初始化识别器
        /// </summary>
        private void InitializeRecognizer()
        {
            CleanupRecognizer();

            // 设置 Vosk 日志级别
            Vosk.Vosk.SetLogLevel(-1); // 禁用 Vosk 内部日志

            // 加载模型
            _model = new Model(ModelPath);
            
            // 创建识别器（16kHz 采样率）
            _recognizer = new VoskRecognizer(_model, AudioCaptureService.TargetSampleRate);
            _recognizer.SetMaxAlternatives(0);
            _recognizer.SetWords(true);

            Log("Vosk 识别器已初始化");
        }

        /// <summary>
        /// 清理识别器资源
        /// </summary>
        private void CleanupRecognizer()
        {
            _recognizer?.Dispose();
            _recognizer = null;

            _model?.Dispose();
            _model = null;
        }

        /// <summary>
        /// 音频数据可用回调
        /// </summary>
        private void OnAudioDataAvailable(object? sender, AudioDataEventArgs e)
        {
            if (_recognizer == null || e.BytesRecorded == 0)
                return;

            try
            {
                // 将音频数据送入识别器
                if (_recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
                {
                    // 获取完整识别结果
                    var result = _recognizer.Result();
                    ProcessRecognitionResult(result, false);
                }
                else
                {
                    // 获取部分识别结果（可选，用于实时显示）
                    // var partialResult = _recognizer.PartialResult();
                }
            }
            catch (Exception ex)
            {
                Log($"处理音频数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理识别结果
        /// </summary>
        private void ProcessRecognitionResult(string jsonResult, bool isFinal)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonResult);
                var root = doc.RootElement;

                string? text = null;
                
                if (root.TryGetProperty("text", out var textElement))
                {
                    text = textElement.GetString();
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    var result = new SpeechResult
                    {
                        Text = text,
                        Timestamp = DateTime.Now,
                        IsFinal = isFinal
                    };

                    Log($"识别结果: {text}");
                    TextRecognized?.Invoke(this, result);
                }
            }
            catch (Exception ex)
            {
                Log($"解析识别结果失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置状态并触发事件
        /// </summary>
        private void SetState(SpeechServiceState newState)
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
            Debug.WriteLine($"[SpeechService] {message}");
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
                CleanupRecognizer();
            }

            _disposed = true;
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~SpeechService()
        {
            Dispose(false);
        }

        #endregion
    }


    #region Supporting Types

    /// <summary>
    /// 语音识别服务状态
    /// </summary>
    public enum SpeechServiceState
    {
        /// <summary>
        /// 已停止
        /// </summary>
        Stopped,

        /// <summary>
        /// 正在识别
        /// </summary>
        Recognizing
    }

    /// <summary>
    /// 语音识别结果
    /// </summary>
    public class SpeechResult
    {
        /// <summary>
        /// 识别的文本
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 是否为最终结果
        /// </summary>
        public bool IsFinal { get; set; }

        /// <summary>
        /// 置信度（0-1，如果可用）
        /// </summary>
        public float Confidence { get; set; } = 1.0f;
    }

    /// <summary>
    /// 模型下载状态
    /// </summary>
    public enum ModelDownloadStatus
    {
        /// <summary>
        /// 正在下载
        /// </summary>
        Downloading,

        /// <summary>
        /// 正在解压
        /// </summary>
        Extracting,

        /// <summary>
        /// 已完成
        /// </summary>
        Completed,

        /// <summary>
        /// 失败
        /// </summary>
        Failed
    }

    /// <summary>
    /// 模型下载进度
    /// </summary>
    public class ModelDownloadProgress
    {
        /// <summary>
        /// 下载百分比（0-100，-1 表示未知）
        /// </summary>
        public int Percentage { get; }

        /// <summary>
        /// 当前下载源名称
        /// </summary>
        public string SourceName { get; }

        /// <summary>
        /// 下载状态
        /// </summary>
        public ModelDownloadStatus Status { get; }

        /// <summary>
        /// 已下载字节数
        /// </summary>
        public long DownloadedBytes { get; }

        /// <summary>
        /// 总字节数（-1 表示未知）
        /// </summary>
        public long TotalBytes { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ModelDownloadProgress(int percentage, string sourceName, ModelDownloadStatus status, long downloadedBytes = 0, long totalBytes = -1)
        {
            Percentage = percentage;
            SourceName = sourceName;
            Status = status;
            DownloadedBytes = downloadedBytes;
            TotalBytes = totalBytes;
        }
    }

    #endregion
}
