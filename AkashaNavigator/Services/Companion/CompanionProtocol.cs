using System.Buffers.Binary;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AkashaNavigator.Services.Companion;

internal static class CompanionProtocol
{
    public const int CurrentVersion = 1;
    public const int MaximumPayloadBytes = 256 * 1024;

    public const string Hello = "hello";
    public const string Welcome = "welcome";
    public const string Request = "request";
    public const string Response = "response";
    public const string Shutdown = "shutdown";

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

internal sealed record CompanionEnvelope
{
    public required string Type { get; init; }

    public string? CorrelationId { get; init; }

    public string? Method { get; init; }

    public JsonElement? Payload { get; init; }

    public int? ProtocolVersion { get; init; }

    public string? Token { get; init; }

    public string? WorkerVersion { get; init; }

    public int? ParentProcessId { get; init; }

    public bool? Accepted { get; init; }

    public CompanionError? Error { get; init; }
}

internal sealed record CompanionError(string Code, string Message);

internal sealed class CompanionFraming(int maximumPayloadBytes = CompanionProtocol.MaximumPayloadBytes)
{
    public int MaximumPayloadBytes { get; } = maximumPayloadBytes > 0
        ? maximumPayloadBytes
        : throw new ArgumentOutOfRangeException(nameof(maximumPayloadBytes));

    public async ValueTask WriteAsync(
        Stream stream,
        CompanionEnvelope message,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, CompanionProtocol.JsonOptions);
        if (payload.Length is <= 0 || payload.Length > MaximumPayloadBytes)
        {
            throw new InvalidDataException(
                $"Companion payload length {payload.Length} is outside the allowed range 1-{MaximumPayloadBytes}.");
        }

        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<CompanionEnvelope> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var header = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (payloadLength is <= 0 || payloadLength > MaximumPayloadBytes)
        {
            throw new InvalidDataException(
                $"Companion payload length {payloadLength} is outside the allowed range 1-{MaximumPayloadBytes}.");
        }

        var payload = new byte[payloadLength];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<CompanionEnvelope>(payload, CompanionProtocol.JsonOptions)
               ?? throw new JsonException("Companion payload deserialized to null.");
    }
}
