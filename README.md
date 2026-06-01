# AI Minutes Assistant – Backend

An ASP.NET Core 9 API that records meetings via a [Recall.ai](https://www.recall.ai/) bot,
stores the resulting audio in [Backblaze B2](https://www.backblaze.com/b2/), and produces
AI-generated meeting minutes.

---

## What Has Been Implemented

### 1. Meeting Management (`/api/meeting`)

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/meeting` | Create a new meeting |
| `GET` | `/api/meeting` | List all meetings |
| `GET` | `/api/meeting/{id}` | Get a single meeting |
| `GET` | `/api/meeting/user/{creatorId}` | Meetings for a specific user |
| `PUT` | `/api/meeting` | Update meeting details |
| `DELETE` | `/api/meeting/{id}` | Soft-delete a meeting |
| `POST` | `/api/meeting/{id}/bot` | **Dispatch a Recall.ai recording bot** |

When a bot is dispatched (`POST /api/meeting/{id}/bot`):
- Recall.ai is called to create a bot that joins the supplied meeting URL.
- The bot's ID is saved to `Meeting.RecallBotId` in the database.
- The meeting status is set to `Scheduled`.

### 2. Webhook Endpoint – Full Bot Lifecycle (`POST /api/webhook/recall`)

Recall.ai fires webhook events as the bot progresses through its lifecycle.
The handler now processes **all** meaningful events:

| Recall.ai Event | Action taken |
|-----------------|-------------|
| `bot.joining_call` | Sets `Meeting.Status = Scheduled` |
| `bot.in_call_not_recording` | Logs; no status change (bot waiting for host) |
| `bot.in_call_recording` | Sets `Meeting.Status = Recording` |
| `bot.done` | **Downloads audio → uploads to Backblaze B2 → creates `AudioFile` record → sets `Meeting.Status = Completed`** |
| `bot.fatal` | Sets `Meeting.Status = Failed`; logs error |
| *(anything else)* | Acknowledged and ignored |

### 3. Audio Storage Flow (triggered by `bot.done`)

1. Recall.ai provides signed media URLs via `GET /recording/{id}/media/`.
2. The audio stream is downloaded from the pre-signed URL.
3. The stream is uploaded to Backblaze B2 under the `audio/` prefix using
   the S3-compatible API (`AWSSDK.S3`).
4. An `AudioFile` row is inserted that captures:
   - `MeetingId`, `FileName`, `FilePath` (B2 key), `FileUrl`, `ContentType`, `FileSize`
   - `Status = pending` (ready for transcription)
5. `Meeting.AudioUrl` is set to the public B2 file URL and `Meeting.EndedAt` is recorded.

### 4. Audio Upload (manual) (`POST /api/audio/upload`)

Allows direct upload of a pre-recorded audio file for a given meeting,
as an alternative to the automated bot flow.

### 5. Transcription (`/api/transcription`)

Transcribes an audio file linked to a meeting using Groq's Whisper API.
Results are stored in the `Transcripts` table.

### 6. Minutes Generation (`/api/minutes`)

Generates structured meeting minutes from a transcript using the Gemini 2.5 Flash API.
Stores versioned minutes in `Minutes` / `MinutesVersions` tables.

---

## Configuration (`appsettings.json`)

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "<PostgreSQL / Supabase connection string>"
  },
  "BackblazeB2": {
    "ApplicationKeyId": "<your-key-id>",
    "ApplicationKey":   "<your-application-key>",
    "BucketId":         "<your-bucket-id>",
    "BucketName":       "<your-bucket-name>",
    "Endpoint":         "https://s3.<region>.backblazeb2.com"
  },
  "RecallAi": {
    "ApiKey":   "<your-recall-ai-api-key>",
    "BaseUrl":  "https://<region>.recall.ai/api/v1/"
  },
  "Groq": {
    "ApiKey": "<your-groq-api-key>"
  },
  "GeminiApi": {
    "ApiKey": "<your-gemini-api-key>",
    "Url":    "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent"
  }
}
```

### Recall.ai Webhook Setup

In the Recall.ai dashboard register the following webhook URL:

```
POST https://<your-domain>/api/webhook/recall
```

Enable at minimum these event types:
- `bot.joining_call`
- `bot.in_call_not_recording`
- `bot.in_call_recording`
- `bot.done`
- `bot.fatal`

---

## Running Locally

```bash
# 1. Restore packages
dotnet restore

# 2. Apply database migrations
dotnet ef database update

# 3. Run the API (Scalar UI available at /scalar/v1)
dotnet run
```

---

## What's Next to Implement

### High Priority

1. **Webhook signature verification**  
   Recall.ai signs every webhook request. Validate the `X-Recall-Signature` header
   before processing any payload to prevent spoofed events.

2. **Automatic transcription after audio upload**  
   Once `bot.done` stores the audio, enqueue a background job (e.g. using
   `IHostedService` + a channel, or Hangfire) to automatically call the
   Groq Whisper transcription endpoint.

3. **Automatic minutes generation after transcription**  
   Chain onto step 2: once transcription is complete, call the Gemini endpoint
   to produce draft minutes and notify stakeholders.

4. **Authentication & authorisation**  
   Add JWT bearer authentication so that API routes require a valid token.
   Protect `POST /api/meeting/{id}/bot` so only the meeting creator can dispatch a bot.

5. **Pagination on list endpoints**  
   `GET /api/meeting` currently returns every row. Add cursor- or page-based
   pagination with a `perPage` cap.

### Medium Priority

6. **Audio duration extraction**  
   Populate `Meeting.AudioDurationSecs` from the recording metadata returned
   by Recall.ai, or by inspecting the downloaded audio stream.

7. **Email notifications**  
   Send an email (via SendGrid / similar) when minutes are ready for approval,
   and when they have been approved or rejected.

8. **Minutes approval workflow**  
   Expose `PUT /api/minutes/{id}/approve` and `PUT /api/minutes/{id}/reject`
   endpoints that set `Meeting.Status` to `Approved` / `Rejected` and record
   `ApprovedBy` / `ApprovedAt`.

9. **Retry logic for Backblaze uploads**  
   Wrap `IStorage.UploadAsync` in a Polly retry policy to handle transient
   network failures without losing audio data.

### Future / Nice-to-Have

10. **Real-time status updates via SignalR**  
    Push bot lifecycle events to a connected frontend so users see live
    "Joining → Recording → Processing → Done" status changes without polling.

11. **Multi-region bot support**  
    Allow the caller to specify a Recall.ai region when dispatching the bot
    to minimise latency for geographically distributed meetings.

12. **Storage provider abstraction**  
    The `IStorage` interface already allows this — add an Azure Blob or AWS S3
    implementation switchable via configuration.
