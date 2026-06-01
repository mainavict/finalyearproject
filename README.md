# AI Minutes Assistant – Backend

An ASP.NET Core 9 API that records meetings via a [Recall.ai](https://www.recall.ai/) bot,
stores the resulting audio in [Backblaze B2](https://www.backblaze.com/b2/), and produces
AI-generated meeting minutes.

---

## Setup Guide

Follow these steps to get the project running from scratch.

### Prerequisites

| Tool | Minimum version |
|------|-----------------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.0 |
| [PostgreSQL](https://www.postgresql.org/) or a [Supabase](https://supabase.com/) project | – |
| [EF Core CLI](https://learn.microsoft.com/ef/core/cli/dotnet) | 9.0 |

Install the EF Core CLI tool once if you haven't already:

```bash
dotnet tool install --global dotnet-ef
```

---

### 1. Clone the repository

```bash
git clone <repository-url>
cd finalyearproject
```

---

### 2. Create your configuration file

The `appsettings.json` file holds all secrets and connection strings.
Copy the template below, fill in your own values, and save it as `appsettings.json`
in the project root (alongside `Program.cs`).

```jsonc
{
  "ConnectionStrings": {
    // PostgreSQL / Supabase connection string
    "DefaultConnection": "Host=<host>;Port=5432;Database=postgres;Username=<user>;******;SSL Mode=Require;Trust Server Certificate=true"
  },

  "BackblazeB2": {
    // keyID shown when you create an application key in the Backblaze dashboard
    "ApplicationKeyId": "<your-application-key-id>",
    // secret key shown once at creation time
    "ApplicationKey":   "<your-application-key>",
    // bucket ID (visible in the bucket details panel)
    "BucketId":         "<your-bucket-id>",
    // bucket name you chose when creating the bucket
    "BucketName":       "<your-bucket-name>",
    // S3-compatible endpoint for your bucket's region, e.g.:
    //   us-west-004  → https://s3.us-west-004.backblazeb2.com
    //   eu-central-003 → https://s3.eu-central-003.backblazeb2.com
    "Endpoint":         "https://s3.<region>.backblazeb2.com"
  },

  "RecallAi": {
    // API key from https://app.recall.ai → Settings → API Keys
    "ApiKey":  "<your-recall-ai-api-key>",
    // Use the region closest to your users, e.g. https://eu-central-1.recall.ai/api/v1/
    "BaseUrl": "https://<region>.recall.ai/api/v1/"
  },

  "Groq": {
    // API key from https://console.groq.com → API Keys
    "ApiKey": "<your-groq-api-key>"
  },

  "GeminiApi": {
    // API key from https://aistudio.google.com → Get API key
    "ApiKey": "<your-gemini-api-key>",
    "Url":    "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent"
  },

  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

> **Security tip:** Add `appsettings.json` to your `.gitignore` to avoid committing real credentials.  
> Use `appsettings.Development.json` (already git-ignored by the standard .NET template) for local overrides,
> or use [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) in development:
> ```bash
> dotnet user-secrets init
> dotnet user-secrets set "BackblazeB2:ApplicationKey" "<value>"
> # …repeat for each secret
> ```

---

### 3. Set up Backblaze B2

1. Log in to [backblaze.com](https://www.backblaze.com/) and open **B2 Cloud Storage**.
2. Click **Create a Bucket** – choose a unique name (e.g. `minutesassistant`).
   - Set **Files in Bucket are** → **Public** if you want direct-link playback, or **Private** to require signed URLs.
3. Note the **Bucket ID** and **Endpoint** shown in the bucket details panel.
4. Go to **Application Keys** → **Add a New Application Key**.
   - Restrict it to your bucket, and grant **Read and Write** access.
   - Copy the **keyID** and **applicationKey** immediately (the key is shown only once).
5. Enter all four values (`ApplicationKeyId`, `ApplicationKey`, `BucketId`, `BucketName`, `Endpoint`) into `appsettings.json`.

---

### 4. Set up Recall.ai

1. Sign up at [app.recall.ai](https://app.recall.ai).
2. From **Settings → API Keys**, create a new key and paste it into `appsettings.json` → `RecallAi:ApiKey`.
3. Set `RecallAi:BaseUrl` to the region endpoint closest to your meetings (e.g. `https://eu-central-1.recall.ai/api/v1/`).

#### Register the webhook

Recall.ai must be told where to send bot-lifecycle events.

1. In the Recall.ai dashboard open **Webhooks → Add endpoint**.
2. Set the URL to your deployed API:
   ```
   https://<your-domain>/api/webhook/recall
   ```
   For local development use a tunnel such as [ngrok](https://ngrok.com/):
   ```bash
   ngrok http 5000
   # copy the https://<id>.ngrok.io URL → https://<id>.ngrok.io/api/webhook/recall
   ```
3. Enable **at minimum** the following event types:
   - `bot.joining_call`
   - `bot.in_call_not_recording`
   - `bot.in_call_recording`
   - `bot.done`
   - `bot.fatal`
4. Save the endpoint.  
   When a bot finishes recording (`bot.done`), Recall.ai will POST to this URL and the API will automatically download the audio and upload it to Backblaze B2.

---

### 5. Apply database migrations

```bash
dotnet ef database update
```

This creates all tables in the PostgreSQL database specified by `ConnectionStrings:DefaultConnection`.

---

### 6. Run the API

```bash
# Restore NuGet packages
dotnet restore

# Start the development server
dotnet run
```

The API will be available at:

| URL | Description |
|-----|-------------|
| `https://localhost:5001` | HTTPS |
| `http://localhost:5000` | HTTP |
| `https://localhost:5001/scalar/v1` | Interactive API docs (Scalar UI) |

---

### 7. Quick smoke test

Dispatch a recording bot to a test meeting:

```bash
# 1. Create a meeting
curl -X POST https://localhost:5001/api/meeting \
  -H "Content-Type: application/json" \
  -d '{"title":"Test meeting","creatorId":"<user-guid>","meetingUrl":"https://meet.google.com/xxx-yyyy-zzz"}'

# 2. Send the bot (replace <meeting-id> with the ID returned above)
curl -X POST https://localhost:5001/api/meeting/<meeting-id>/bot
```

Once the bot leaves the call Recall.ai will fire `bot.done` → your webhook stores the
audio in Backblaze B2 and the `AudioFiles` table is populated.

---

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
