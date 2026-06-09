using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace finalyearproject.DTOs;

// ── Request sent to Recall.ai to create a bot ─────────────────────────────────
public record RecallBotRequest
{
    [JsonPropertyName("meeting_url")]
    public string MeetingUrl { get; init; } = string.Empty;

    [JsonPropertyName("bot_name")]
    public string BotName { get; init; } = "Minutes Assistant";

    [JsonPropertyName("recording_config")]
    public RecallRecordingConfig RecordingConfig { get; init; } = new();
}

public record RecallRecordingConfig
{
    [JsonPropertyName("transcript")]
    public RecallTranscriptConfig Transcript { get; init; } = new();
}

public record RecallTranscriptConfig
{
    [JsonPropertyName("provider")]
    public RecallTranscriptProvider Provider { get; init; } = new();
}

// UPDATED: Use Recall.ai's built-in streaming transcription (works out of the box)
public record RecallTranscriptProvider
{
    [JsonPropertyName("recallai_streaming")]
    public RecallAiStreamingConfig RecallAiStreaming { get; init; } = new();
}

public record RecallAiStreamingConfig
{
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = "prioritize_accuracy";

    [JsonPropertyName("language_code")]
    public string LanguageCode { get; init; } = "auto";
}

public record RecallBotResponse(
    [property: JsonPropertyName("id")] string Id,

    [property: JsonPropertyName("meeting_url")] object? MeetingUrl = null,
    
    [property: JsonPropertyName("status_changes")] List<RecallStatusChange>? StatusChanges = null
);

public record RecallStatusChange(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("created_at")] DateTime? CreatedAt = null
);

// ── DTO returned to the caller after sending the bot ─────────────────────────
public record SendBotResponseDto(
    string BotId,
    Guid MeetingId,
    string Message
);

// ── Recall.ai webhook payload ─────────────────────────────────────────────────
public record RecallWebhookPayload(
    [property: JsonPropertyName("event")] string Event,
    [property: JsonPropertyName("data")] RecallWebhookData? Data = null
);

public record RecallWebhookData(
    [property: JsonPropertyName("bot")] RecallBotData? Bot = null
);

public record RecallBotData(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("meeting_url")] string? MeetingUrl = null,
    [property: JsonPropertyName("status_changes")] List<RecallStatusChange>? StatusChanges = null,
    [property: JsonPropertyName("outputs")] RecallBotOutputs? Outputs = null
);

public record RecallBotOutputs(
    [property: JsonPropertyName("recordings")] List<RecallRecordingRef>? Recordings = null
);

public record RecallRecordingRef(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("started_at")] DateTime? StartedAt = null,
    [property: JsonPropertyName("ends_at")] DateTime? EndsAt = null
);

// ── Recall.ai recording media response ───────────────────────────────────────
public record RecallRecordingMedia(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("media")] RecallMediaLinks? Media = null
);

public record RecallMediaLinks(
    [property: JsonPropertyName("audio")] string? Audio = null,
    [property: JsonPropertyName("video")] string? Video = null
);

// ── Request from your frontend to start a meeting ───────────────────────────
public record StartMeetingRequest(
    string MeetingUrl,
    Guid InternalMeetingId
);