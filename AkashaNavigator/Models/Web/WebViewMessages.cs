using System.Text.Json.Serialization;

namespace AkashaNavigator.Models.Web
{
    /// <summary>
    /// WebView 消息类型常量
    /// </summary>
    public static class WebViewMessageTypes
    {
        public const string PlayerReady = "player.ready";
        public const string VideoProgress = "video.progress";
        public const string PlayerError = "player.error";
        public const string VideoLoaded = "video.loaded";
        public const string PlaybackStateChanged = "player.stateChanged";
        public const string VolumeChanged = "player.volumeChanged";
        public const string SubtitleDetected = "subtitle.detected";
    }

    /// <summary>
    /// 视频进度消息
    /// </summary>
    public record VideoProgressMessage(
        [property: JsonPropertyName("value")] double Value,
        [property: JsonPropertyName("duration")] double Duration
    );

    /// <summary>
    /// 播放器错误消息
    /// </summary>
    public record PlayerErrorMessage(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message
    );

    /// <summary>
    /// 视频加载完成消息
    /// </summary>
    public record VideoLoadedMessage(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("duration")] double Duration,
        [property: JsonPropertyName("url")] string Url
    );

    /// <summary>
    /// 播放状态变化消息
    /// </summary>
    public record PlaybackStateChangedMessage(
        [property: JsonPropertyName("isPlaying")] bool IsPlaying,
        [property: JsonPropertyName("volume")] double Volume
    );

    /// <summary>
    /// 音量变化消息
    /// </summary>
    public record VolumeChangedMessage(
        [property: JsonPropertyName("volume")] double Volume
    );

    /// <summary>
    /// 字幕检测消息
    /// </summary>
    public record SubtitleDetectedMessage(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("position")] double Position
    );
}
