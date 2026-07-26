# AI Minutes Assistant – Backend

An ASP.NET Core 9 API that records meetings via a [Recall.ai](https://www.recall.ai/) bot,
stores the resulting audio in [Backblaze B2](https://www.backblaze.com/b2/), and produces
AI-generated meeting minutes. Payments are handled via the M-Pesa Daraja API, and
real-time notifications are delivered over SignalR.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | ASP.NET Core 9 |
| Database | PostgreSQL (Supabase) via Entity Framework Core 9 |
| Audio Storage | Backblaze B2 (S3-compatible) |
| Recording Bots | Recall.ai |
| Transcription | AssemblyAI |
| AI Minutes | Google Gemini 2.5 Flash |
| Authentication | JWT ****** Clerk (external IdP sync) |
| Real-time | SignalR |
| Payments | M-Pesa Daraja STK Push |
| API Docs | Scalar UI (`/scalar/v1`) |
| Health Check | `/health` |
| Containerisation | Docker / Docker Compose |

---

## Implemented Features

### 1. Authentication (`/api/auth`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/auth/register` | Public | Register a new user with email & password |
| `POST` | `/api/auth/login` | Public | Log in, receive a JWT access token |
| `POST` | `/api/auth/logout` | Public | Log out (token invalidation on client) |
| `POST` | `/api/auth/sync-clerk-user` | Public | Upsert a user record from a Clerk JWT payload |

JWT tokens are read from either a cookie (`auth_token`) or a query-string parameter
(`access_token`) to support both browser and SignalR clients.

---

### 2. User Management (`/api/users`)

All routes require a valid JWT (`[Authorize]`).

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/users` | Create a user |
| `GET` | `/api/users` | List all users |
| `GET` | `/api/users/{id}` | Get a single user |
| `PUT` | `/api/users/{id}` | Update user details |
| `DELETE` | `/api/users/{id}` | Soft-delete a user |

---

### 3. Meeting Management (`/api/meeting`)

All routes require a valid JWT.

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/meeting` | Create a new meeting |
| `GET` | `/api/meeting` | List all meetings |
| `GET` | `/api/meeting/{id}` | Get a single meeting |
| `GET` | `/api/meeting/my-meetings` | Meetings belonging to the current user |
| `PUT` | `/api/meeting` | Update meeting details |
| `DELETE` | `/api/meeting/{id}` | Soft-delete a meeting |
| `POST` | `/api/meeting/start` | **Immediately dispatch a Recall.ai bot** |

---

### 4. Meeting Scheduler Worker (background service)

A hosted `BackgroundService` (`MeetingSchedulerWorker`) runs every **10 seconds** and
automatically dispatches a Recall.ai recording bot **1 minute before** a meeting's
`ScheduledAt` time.

- Uses a **Polly retry policy** (3 attempts, exponential back-off) for Recall.ai calls.
- Implements a **circuit breaker** – after 5 consecutive failures it pauses for 5 minutes.
- Only processes meetings within a 24-hour look-ahead window.

---

### 5. Recall.ai Webhook (`POST /api/webhook/recall`)

Incoming requests are **signature-verified** using HMAC-SHA256 (`RecallWebhookVerifier`)
before any payload is processed.

| Event | Action |
|-------|--------|
| `recording.done` | Downloads audio/video → uploads to Backblaze B2 → creates `AudioFile` record with `Status = pending` |
| `participant_events.done` | Fetches participant events from Recall.ai and saves them to the `Participants` table |
| `bot.call_ended` | Records `Meeting.EndedAt` |
| `bot.done` | Sets `Meeting.Status = Completed`; sends a SignalR push to the meeting creator |

---

### 6. Audio Storage Flow (triggered automatically)

1. Recall.ai provides signed media URLs (audio preferred; video as fallback).
2. The stream is downloaded and uploaded to Backblaze B2 under the `audio/` prefix
   using the S3-compatible API (`AWSSDK.S3`).
3. An `AudioFile` row is created with `Status = pending`.
4. `Meeting.AudioUrl` and `Meeting.AudioMimeType` are updated.
5. The **TranscriptionWorker** (see below) picks up the pending file automatically.

---

### 7. Audio Upload (manual) (`/api/audio`)

All routes require a valid JWT.

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/audio/upload` | Stream-upload an audio file directly (no size limit) |
| `GET` | `/api/audio/presigned-url` | Obtain a B2 presigned URL for direct client-side upload (valid 15 min) |
| `POST` | `/api/audio/confirm-upload` | Register an `AudioFile` record after a client-side upload |
| `GET` | `/api/audio` | List all audio files |
| `GET` | `/api/audio/user/{id}` | Audio files belonging to the current user |
| `GET` | `/api/audio/meeting/{meetingId}` | Audio file for a specific meeting |

---

### 8. Transcription Worker (background service)

A hosted `BackgroundService` (`TranscriptionWorker`) polls every **5 seconds** for
`AudioFile` records with `Status = pending` and transcribes them via **AssemblyAI**.

- Sends real-time SignalR notifications at each stage: `processing`, `transcribed`, `failed`.
- Transcript text is stored in the `Transcripts` table.

Manual transcription endpoint: `POST /api/transcription/{audioFileId}/transcribe`

---

### 9. Minutes Generation & Management (`/api/minutes`)

All routes require a valid JWT.

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/minutes/generate` | Generate draft minutes from a transcript using **Gemini 2.5 Flash** |
| `GET` | `/api/minutes/{meetingId}` | Get minutes, transcript, participants, and audio details for a meeting |
| `GET` | `/api/minutes/all` | List all minutes for the current user |
| `PUT` | `/api/minutes/{meetingId}/minutes` | Update/manually edit minutes |

Generated minutes are stored as JSON in `Minutes.DraftJson`. Both a primary model
(`gemini-2.5-flash`) and a fallback model (`gemini-1.5-flash`) are configured.

---

### 10. Participant Management (`/api/participant`)

All routes require a valid JWT.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/participant/meeting/{meetingId}` | List participants for a meeting |
| `POST` | `/api/participant/meeting/{meetingId}` | Add a participant manually |
| `PUT` | `/api/participant/{participantId}` | Update participant details |
| `DELETE` | `/api/participant/{participantId}` | Remove a participant |

Participants are also extracted automatically from Recall.ai `participant_events.done`
webhook events.

---

### 11. M-Pesa Daraja Payments (`/api/daraja`)

Enables subscription and top-up purchases via M-Pesa STK Push.

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/daraja/stk-push` | Required | Initiate an STK Push payment |
| `POST` | `/api/daraja/callback` | Public | M-Pesa callback — updates payment & user subscription |
| `GET` | `/api/daraja/status/{checkoutRequestId}` | Required | Poll payment status |
| `GET` | `/api/daraja/history` | Required | User's completed payment history |
| `GET` | `/api/daraja/plans` | Public | List active subscription plans |

Subscription tiers are managed dynamically via the `SubscriptionPlans` table.
On successful payment the user's `SubscriptionTier`, `MonthlyMinuteLimit`, and
`SubscriptionExpiryDate` are updated automatically.

---

### 12. Real-time Notifications (SignalR)

A `NotificationHub` is mounted at `/hubs/notifications` (requires JWT).

- Users are automatically placed in a group matching their `UserId` on connect.
- The `TranscriptionWorker` and webhook handler push status updates via
  `INotificationService.SendTranscriptionUpdateAsync`.

---

### 13. Webhook Signature Verification

`RecallWebhookVerifier` validates every incoming Recall.ai webhook using HMAC-SHA256.
The secret is read from `RecallAi:WebhookSecret` in configuration.
Requests with missing or invalid signatures are rejected with `401 Unauthorized`.

---

## Data Models

| Model | Key Fields |
|-------|-----------|
| `User` | `UserId`, `FullName`, `Email`, `ClerkUserId`, `SubscriptionTier`, `MonthlyMinuteLimit`, `MinutesUsedThisMonth`, `SubscriptionExpiryDate` |
| `Meeting` | `Id`, `CreatorId`, `Title`, `Status` (`Created`→`Scheduled`→`Recording`→`Completed`/`Failed`/`Approved`/`Rejected`), `RecallBotId`, `ScheduledAt`, `EndedAt`, `ApprovedBy`, `ApprovedAt` |
| `AudioFile` | `Id`, `MeetingId`, `FileName`, `FilePath`, `FileUrl`, `ContentType`, `FileSize`, `Status` (`pending`/`processing`/`transcribed`/`failed`) |
| `Transcript` | `Id`, `MeetingId`, `RawText`, `WhisperModel`, `CreatedAt` |
| `Minutes` | `Id`, `MeetingId`, `DraftJson`, `LlmModel`, `Status`, `CreatedAt`, `UpdatedAt` |
| `Participant` | `Id`, `MeetingId`, `Name`, `Email`, `Role`, `IsHost`, `JoinedAt`, `LeftAt`, `DurationSeconds` |
| `Payment` | `Id`, `UserId`, `CheckoutRequestID`, `PlanType`, `Amount`, `Status` |
| `SubscriptionPlan` | `PlanCode`, `DisplayName`, `PriceKSH`, `IncludedMinutes`, `DurationDays` |

---

## Configuration (`appsettings.json`)

> ⚠️ **Never commit real secrets.** Use environment variables or a secrets manager in
> production. The keys below should be treated as placeholders.

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "<PostgreSQL / Supabase connection string>"
  },
  "JwtSettings": {
    "SecretKey":        "<long-random-secret>",
    "Issuer":           "MeetingAssistantAPI",
    "Audience":         "MeetingAssistantClient",
    "ExpiryInMinutes":  60
  },
  "BackblazeB2": {
    "ApplicationKeyId": "<your-key-id>",
    "ApplicationKey":   "<your-application-key>",
    "BucketId":         "<your-bucket-id>",
    "BucketName":       "<your-bucket-name>",
    "Endpoint":         "https://s3.<region>.backblazeb2.com"
  },
  "RecallAi": {
    "ApiKey":        "<your-recall-ai-api-key>",
    "BaseUrl":       "https://<region>.recall.ai/api/v1/",
    "WebhookSecret": "<whsec_...base64-secret>"
  },
  "AssemblyAI": {
    "ApiKey": "<your-assemblyai-api-key>"
  },
  "GeminiApi": {
    "ApiKey":        "<your-gemini-api-key>",
    "Url":           "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent",
    "Model":         "gemini-2.5-flash",
    "FallbackUrl":   "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent",
    "FallbackModel": "gemini-1.5-flash"
  },
  "Daraja": {
    "ConsumerKey":    "<mpesa-consumer-key>",
    "ConsumerSecret": "<mpesa-consumer-secret>",
    "Shortcode":      "<business-shortcode>",
    "Passkey":        "<lipa-na-mpesa-passkey>",
    "CallbackUrl":    "https://<your-domain>/api/daraja/callback"
  }
}
```

### Recall.ai Webhook Setup

In the Recall.ai dashboard register the following webhook URL:

```
POST https://<your-domain>/api/webhook/recall
```

Enable at minimum these event types:
- `recording.done`
- `participant_events.done`
- `bot.call_ended`
- `bot.done`

---

## Running Locally

```bash
# 1. Restore packages
dotnet restore

# 2. Apply database migrations
dotnet ef database update

# 3. Run the API (Scalar UI available at /scalar/v1, health check at /health)
dotnet run
```

### Docker

```bash
docker-compose up --build
```

---

## What's Still To Do

### Medium Priority

1. **Email notifications**  
   Send an email (via SendGrid or similar) when minutes are ready for approval
   and when they are approved or rejected.

2. **Minutes approval endpoints**  
   Expose `PUT /api/minutes/{id}/approve` and `PUT /api/minutes/{id}/reject`
   that set `Meeting.Status` to `Approved` / `Rejected` and record
   `ApprovedBy` / `ApprovedAt` (the model fields already exist).

3. **Pagination on list endpoints**  
   `GET /api/meeting` and `GET /api/users` currently return every row. Add
   cursor- or page-based pagination with a `perPage` cap.

4. **Audio duration extraction**  
   Populate `Meeting.AudioDurationSecs` from the Recall.ai recording metadata
   or by inspecting the downloaded audio stream (`AudioFile.Duration`).

### Future / Nice-to-Have

5. **Multi-region bot support**  
   Allow the caller to specify a Recall.ai region when dispatching the bot
   to minimise latency for geographically distributed meetings.

6. **Storage provider abstraction**  
   The `IStorage` interface already allows this — add an Azure Blob or AWS S3
   implementation switchable via configuration.
