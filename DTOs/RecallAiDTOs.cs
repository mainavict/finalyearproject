using System.Text.Json.Serialization;

namespace finalyearproject.DTOs;

// ── Request sent to Recall.ai to create a bot ─────────────────────────────────
public class RecallBotRequest
{
    [JsonPropertyName("meeting_url")]
    public string MeetingUrl { get; set; } = string.Empty;

    [JsonPropertyName("bot_name")]
    public string BotName { get; set; } = "Minutes Assistant";

    [JsonPropertyName("recording_config")]
    public RecallRecordingConfig RecordingConfig { get; set; } = new();
}

public class RecallRecordingConfig
{
    [JsonPropertyName("transcript")]
    public RecallTranscriptConfig Transcript { get; set; } = new();
}

public class RecallTranscriptConfig
{
    [JsonPropertyName("provider")]
    public RecallTranscriptProvider Provider { get; set; } = new();
}

public class RecallTranscriptProvider
{
    [JsonPropertyName("assembly_ai")]
    public object? AssemblyAi { get; set; } = new { };
}

// ── Response from Recall.ai bot creation ─────────────────────────────────────
public class RecallBotResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("meeting_url")]
    public string MeetingUrl { get; set; } = string.Empty;

    [JsonPropertyName("status_changes")]
    public List<RecallStatusChange>? StatusChanges { get; set; }
}

public class RecallStatusChange
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }
}

// ── DTO returned to the caller after sending the bot ─────────────────────────
public class SendBotResponseDto
{
    public string BotId { get; set; } = string.Empty;
    public Guid MeetingId { get; set; }
    public string Message { get; set; } = string.Empty;
}

// ── Recall.ai webhook payload ─────────────────────────────────────────────────
public class RecallWebhookPayload
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public RecallWebhookData? Data { get; set; }
}

public class RecallWebhookData
{
    [JsonPropertyName("bot")]
    public RecallBotData? Bot { get; set; }
}

public class RecallBotData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("meeting_url")]
    public string? MeetingUrl { get; set; }

    [JsonPropertyName("status_changes")]
    public List<RecallStatusChange>? StatusChanges { get; set; }

    [JsonPropertyName("outputs")]
    public RecallBotOutputs? Outputs { get; set; }
}

public class RecallBotOutputs
{
    [JsonPropertyName("recordings")]
    public List<RecallRecordingRef>? Recordings { get; set; }
}

public class RecallRecordingRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("ends_at")]
    public DateTime? EndsAt { get; set; }
}

// ── Recall.ai recording media response ───────────────────────────────────────
public class RecallRecordingMedia
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("media")]
    public RecallMediaLinks? Media { get; set; }
}

public class RecallMediaLinks
{
    [JsonPropertyName("audio")]
    public string? Audio { get; set; }

    [JsonPropertyName("video")]
    public string? Video { get; set; }
}
