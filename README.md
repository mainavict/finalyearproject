# AI Meeting Minutes Assistant — Backend

> **ASP.NET Core 9 · PostgreSQL (Supabase) · Backblaze B2 · Recall.ai · AssemblyAI · Google Gemini · SignalR**

This is the complete backend for an AI-powered meeting assistant. It automatically joins online meetings via a bot, records the audio, transcribes the speech, and generates professional structured meeting minutes — all without any manual intervention after the initial meeting is scheduled.

---

## Table of Contents

1. [High-Level Architecture](#1-high-level-architecture)
2. [Technology Stack](#2-technology-stack)
3. [Project Structure](#3-project-structure)
4. [Database Models](#4-database-models)
5. [Authentication & Security](#5-authentication--security)
6. [API Endpoints — Complete Reference](#6-api-endpoints--complete-reference)
7. [Background Workers](#7-background-workers)
8. [External Service Integrations](#8-external-service-integrations)
9. [Real-Time Notifications (SignalR)](#9-real-time-notifications-signalr)
10. [The Automated Pipeline — End to End](#10-the-automated-pipeline--end-to-end)
11. [API Response Envelope](#11-api-response-envelope)
12. [Configuration Reference](#12-configuration-reference)
13. [Running Locally](#13-running-locally)
14. [Deploying with Docker](#14-deploying-with-docker)

---

## 1. High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Frontend (Next.js)                        │
│            http://localhost:3000  |  minutesapp.vercel.app       │
└──────────────────────────┬──────────────────────────────────────┘
                           │  HTTPS REST + SignalR WebSocket
┌──────────────────────────▼──────────────────────────────────────┐
│                   ASP.NET Core 9 API  (this repo)                │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────────┐   │
│  │  Controllers │  │   Services   │  │  Background Workers  │   │
│  │  (REST API)  │  │  (Business   │  │  TranscriptionWorker │   │
│  │              │  │   Logic)     │  │  MeetingScheduler    │   │
│  └──────┬───────┘  └──────┬───────┘  └──────────┬──────────┘   │
│         │                 │                      │              │
│  ┌──────▼─────────────────▼──────────────────────▼──────────┐  │
│  │               Entity Framework Core (EF Core 9)           │  │
│  └──────────────────────────┬────────────────────────────────┘  │
│                             │                                    │
│  ┌──────────────────────────▼────────────────────────────────┐  │
│  │           PostgreSQL via Supabase Connection Pool          │  │
│  └───────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
         │               │                │              │
  ┌──────▼──────┐ ┌──────▼──────┐ ┌──────▼──────┐ ┌────▼────────┐
  │  Recall.ai  │ │  AssemblyAI │ │ Google      │ │ Backblaze B2│
  │  (Bot joins │ │  (Speech-to-│ │ Gemini AI   │ │ (Audio file │
  │  meetings & │ │   text      │ │ (Minutes    │ │  storage)   │
  │  records)   │ │  transcribe)│ │  generation)│ │             │
  └─────────────┘ └─────────────┘ └─────────────┘ └─────────────┘
```

### The automated flow in one sentence

> A user schedules a meeting → the **MeetingSchedulerWorker** deploys a Recall.ai bot 1 minute before start → Recall.ai sends webhook events → the backend downloads the recorded audio into Backblaze B2 → the **TranscriptionWorker** detects the new file, sends it to AssemblyAI, and then sends the transcript to Google Gemini → structured meeting minutes are saved to the database → the frontend user receives a real-time SignalR notification.

---

## 2. Technology Stack

| Layer | Technology | Version | Purpose |
|-------|-----------|---------|---------|
| **Runtime** | .NET / ASP.NET Core | 9.0 | Web API host |
| **Database ORM** | Entity Framework Core + Npgsql | 9.0 | PostgreSQL access |
| **Database** | PostgreSQL (Supabase) | — | Persistent storage |
| **Authentication** | JWT ****** | — | Stateless auth |
| **Real-time** | ASP.NET Core SignalR | 9.0 | Live push notifications |
| **API Docs** | Scalar (OpenAPI) | 2.x | Interactive API explorer |
| **Audio Storage** | Backblaze B2 (S3-compatible) | — | Audio file blob store |
| **Meeting Bot** | Recall.ai | v1 | Joins & records Zoom/Meet/Teams |
| **Speech-to-Text** | AssemblyAI | v2 | Audio → raw transcript text |
| **AI Minutes** | Google Gemini 2.5 Flash | — | Transcript → structured minutes |
| **Retry / Resilience** | Polly | 8.x | Exponential backoff & circuit breaker |
| **Password Hashing** | BCrypt.Net-Next | 4.x | Secure user passwords |
| **Containerisation** | Docker | — | Production packaging |

---

## 3. Project Structure

```
finalyearproject/
├── Controllers/                  # HTTP route handlers (thin layer — delegate to services)
│   ├── AuthController.cs         # Register / Login / Logout
│   ├── UserControllers.cs        # CRUD for User accounts
│   ├── MeetingControllers.cs     # Meeting CRUD + bot dispatch
│   ├── AudioControllers.cs       # Audio upload & presigned URL generation
│   ├── TrascriptionControllers.cs# Manual transcription trigger
│   ├── MinutesControllers.cs     # Minutes generation, retrieval & editing
│   ├── ParticipantControllers.cs # Meeting participant management
│   └── RecallWebhookController.cs# Receives & processes Recall.ai lifecycle events
│
├── Services/
│   ├── Interfaces/               # Contracts (IAuthService, Imeeting, IAudio, …)
│   └── Implementation/           # Concrete service classes
│       ├── AuthService.cs        # JWT generation & BCrypt password verify
│       ├── UserServices.cs       # User CRUD
│       ├── MeetingServices.cs    # Meeting CRUD
│       ├── AudioServices.cs      # AudioFile CRUD
│       ├── AudioProcessingService.cs # Orchestrates: download → transcribe → generate minutes
│       ├── AssemblyAITranscriptionService.cs # Upload stream → poll for transcript
│       ├── MinutesServices.cs    # Calls Gemini, cleans + repairs JSON output
│       ├── RecallAiService.cs    # HTTP client for Recall.ai REST API
│       ├── StorageServices.cs    # Backblaze B2 via AWS S3 SDK (presigned URLs, upload)
│       ├── BackblazeB2Service.cs # Backblaze B2 native API (download, signed URLs)
│       ├── ParticipantService.cs # Participant CRUD
│       ├── NotificationService.cs# Wraps SignalR hub for structured push messages
│       └── TranscriberServices.cs# (legacy local file path transcriber)
│
├── Models/                       # EF Core entity classes (database tables)
│   ├── Users.cs
│   ├── Meetings.cs
│   ├── AudioFile.cs
│   ├── Transcript.cs
│   ├── Minutes.cs
│   ├── MinutesVersion.cs
│   ├── Participant.cs
│   ├── RefreshToken.cs
│   ├── EmailOtp.cs
│   └── EmailDeliveryLog.cs
│
├── DTOs/                         # Data Transfer Objects (request/response shapes)
│   ├── AuthDTOs.cs
│   ├── MeetingDTOs.cs
│   ├── AudioDTOs.cs
│   ├── MinutesDtos.cs
│   ├── UserDTOs.cs
│   ├── RecallAiDTOs.cs
│   └── PresignedUploadDtos.cs
│
├── Data/
│   └── AppDbContext.cs           # EF Core DbContext — all DbSets + model configuration
│
├── Workers/                      # Long-running background services (IHostedService)
│   ├── TranscriptionWorker.cs    # Polls for pending audio → triggers full pipeline
│   └── MeetingSchedulerWorker.cs # Deploys Recall.ai bot 1 min before scheduled meetings
│
├── Hubs/
│   └── NotificationHub.cs        # SignalR hub — groups users by their UserId
│
├── Security/
│   └── RecallWebhookVerifier.cs  # HMAC-SHA256 signature verification for Recall webhooks
│
├── Extensions/
│   └── ClaimsPrincipalExtensions.cs # User.GetUserId() helper for controllers
│
├── Wrappers/
│   └── ApiResponse.cs            # Generic { success, message, data, statusCode } envelope
│
├── Migrations/                   # EF Core auto-generated migration files
├── Program.cs                    # Application startup, DI registration, middleware pipeline
├── appsettings.json              # Configuration (connection strings, API keys, JWT)
├── Dockerfile                    # Production container definition
└── finalyearproject.csproj       # .NET project file & NuGet dependencies
```

---

## 4. Database Models

All tables are managed by Entity Framework Core using the code-first approach. The `AppDbContext` defines the schema, relationships, indexes, and query filters.

### 4.1 `User`

```
Users
├── UserId         Guid        PK, auto-generated
├── FullName       string      Required
├── Email          string      Required, Unique index
├── PasswordHash   string      BCrypt hash — NEVER stored in plain text
├── IsActive       bool        Default true; set false to deactivate without deleting
├── IsDeleted      bool        Soft-delete flag
├── CreatedAt      DateTime    UTC timestamp
└── Meetings       ICollection Navigation — a user owns many meetings
```

**Notes:**
- Email uniqueness is enforced at the database level via `HasIndex(u => u.Email).IsUnique()`.
- Soft-deleted users (`IsDeleted = true`) will not appear in login queries.

---

### 4.2 `Meeting`

```
Meetings
├── Id                Guid            PK
├── CreatorId         Guid            FK → Users.UserId
├── ApprovedBy        Guid?           FK → Users.UserId (nullable)
├── Title             string          Required
├── Description       string?
├── Status            MeetingStatus   Enum stored as string
├── MeetingUrl        string?         The Zoom/Teams/Meet join URL
├── AudioUrl          string?         Final public Backblaze B2 URL
├── AudioDurationSecs int?
├── AudioMimeType     string?
├── ScheduledAt       DateTime?
├── EndedAt           DateTime?
├── ApprovedAt        DateTime?
├── RecallBotId       string?         The bot ID returned by Recall.ai
├── CreatedAt         DateTime
├── UpdatedAt         DateTime
├── IsDeleted         bool            Soft-delete (global query filter applied)
├── Creator           User            Navigation
├── Transcript        Transcript?     One-to-one navigation
├── Minutes           Minutes?        One-to-one navigation
├── AudioFiles        ICollection<AudioFile>
└── Participants      ICollection<Participant>
```

**`MeetingStatus` enum values:**
| Value | Meaning |
|-------|---------|
| `Created` | Meeting record exists, no URL or bot yet |
| `Scheduled` | Has a `MeetingUrl` and `ScheduledAt`; bot not yet deployed |
| `Recording` | Bot has joined and is actively recording |
| `Completed` | Bot finished; audio, transcript, and minutes are ready |
| `Failed` | Bot deployment or recording failed |
| `Approved` | Minutes have been reviewed and approved |
| `Rejected` | Minutes were reviewed and rejected |

**Query filter:** `HasQueryFilter(m => !m.IsDeleted)` — all EF Core queries automatically exclude soft-deleted meetings unless you explicitly call `.IgnoreQueryFilters()`.

---

### 4.3 `AudioFile`

```
AudioFiles
├── Id           Guid        PK
├── MeetingId    Guid        FK → Meetings.Id
├── FileName     string      Original file name
├── FilePath     string      Key path inside the Backblaze B2 bucket
├── FileUrl      string      Public CDN URL (https://f004.backblazeb2.com/file/…)
├── ContentType  string      MIME type (audio/mp4, video/mp4, etc.)
├── FileSize     long        Bytes
├── Status       stutus      Enum (see below)
└── Meeting      Meeting     Navigation
```

**`stutus` enum values** *(note: intentional spelling from original code)*:
| Value | Meaning |
|-------|---------|
| `pending` | Audio uploaded; waiting for the TranscriptionWorker to pick it up |
| `prossesing` | Currently being downloaded from B2 and sent to AssemblyAI |
| `transcribed` | Full pipeline complete (transcript + minutes saved) |
| `failed` | An unrecoverable error occurred |
| `minutesgenerated` | Minutes successfully generated (pipeline complete) |
| `minutesgenerationfailed` | Transcription succeeded but minutes generation failed |

---

### 4.4 `Transcript`

```
Transcripts
├── Id           Guid      PK
├── MeetingId    Guid      FK → Meetings.Id (Unique index — one transcript per meeting)
├── RawText      string    The full speech-to-text output from AssemblyAI
├── Segments     string?   JSON array of timed segments (if available)
├── WhisperModel string?   Model used (e.g. "whisper-large-v3")
├── DurationSecs int?
├── WordCount    int?
├── CreatedAt    DateTime
└── Meeting      Meeting   Navigation
```

---

### 4.5 `Minutes`

```
Minutes
├── Id                Guid           PK
├── MeetingId         Guid           FK → Meetings.Id (Unique — one record per meeting)
├── Status            string         "draft", "pending_validation", etc.
├── Version           int            Starts at 1
├── DraftJson         string         Raw JSON string from Gemini (100 MB max)
├── ApprovedHtml      string?        Rendered HTML after approval
├── ApprovedBy        Guid?          FK → Users.UserId
├── ApprovedAt        DateTime?
├── LlmModel          string?        "Gemini Pro", "Manual Edit", etc.
├── PromptTokens      int?
├── CompletionTokens  int?
├── SearchVector      NpgsqlTsVector Full-text search vector (auto-generated from ApprovedHtml)
├── CreatedAt         DateTime
├── UpdatedAt         DateTime
├── Meeting           Meeting        Navigation
├── Approver          User?          Navigation
└── Versions          ICollection<MinutesVersion>
```

**Full-text search:** The `SearchVector` column is automatically populated by PostgreSQL using a GIN index on `ApprovedHtml`. This enables fast keyword searching across all approved minutes.

---

### 4.6 `Participant`

```
Participants
├── Id              Guid      PK
├── MeetingId       Guid      FK → Meetings.Id (cascade delete)
├── Name            string    Display name (max 200 chars)
├── Email           string?   Optional — Recall.ai doesn't always provide it
├── Role            string?   "Host" | "Attendee" | "Presenter"
├── IsHost          bool
├── JoinedAt        DateTime
├── LeftAt          DateTime?
├── DurationSeconds int       Seconds they were in the meeting
├── CreatedAt       DateTime
└── Meeting         Meeting   Navigation
```

---

### 4.7 Supporting Tables

| Table | Purpose |
|-------|---------|
| `MinutesVersions` | Immutable history of every edit made to a `Minutes` record |
| `EmailOtps` | One-time passwords for email verification flows |
| `EmailDeliveryLogs` | Tracks every email sent (SendGrid message IDs, status, etc.) |
| `RefreshTokens` | (model exists for future refresh-token rotation support) |

---

## 5. Authentication & Security

### 5.1 JWT ******

All protected endpoints require a valid JWT in one of two places (checked in order):

1. **HTTP Cookie** named `auth_token`
2. **Query string** parameter `access_token` (used by SignalR since WebSockets can't send custom headers)

```
Token structure (HS256):
  Issuer    : MeetingAssistantAPI
  Audience  : MeetingAssistantClient
  Expiry    : 60 minutes from issue time
  Claims    : NameIdentifier (UserId GUID), Email, Name
```

The `ClaimsPrincipalExtensions.GetUserId()` method extracts the `NameIdentifier` claim from the JWT and returns it as a `Guid`. Every controller that needs the logged-in user's ID calls `User.GetUserId()`.

### 5.2 Password Security

Passwords are **never stored in plain text**. The `Microsoft.AspNetCore.Identity.PasswordHasher<User>` (BCrypt-based) is used:

```
Registration : password → PasswordHasher.HashPassword(user, rawPassword) → stored hash
Login        : PasswordHasher.VerifyHashedPassword(user, storedHash, raw) → Pass/Fail/Rehash
```

### 5.3 Recall.ai Webhook Signature Verification

Every webhook POST from Recall.ai includes three headers:
- `Webhook-Id` (or `svix-id`) — unique message ID
- `Webhook-Timestamp` (or `svix-timestamp`) — Unix timestamp
- `Webhook-Signature` (or `svix-signature`) — one or more `v1,<base64-sig>` values

The `RecallWebhookVerifier.VerifySignature()` method:

1. Strips the `whsec_` prefix from the secret and Base64-decodes it to get the raw HMAC key.
2. Constructs the signing string: `"{msgId}.{msgTimestamp}.{rawPayload}"`.
3. Computes HMAC-SHA256 of the signing string using the decoded key.
4. Compares against each signature in the header using **`CryptographicOperations.FixedTimeEquals`** — a timing-safe comparison that prevents timing-based attacks.

If the signature does not match, the controller immediately returns `401 Unauthorized` and discards the payload.

### 5.4 CORS Policy

CORS is restricted to two origins:

```
http://localhost:3000           (local Next.js development)
https://minutesapp.vercel.app   (production frontend)
```

`AllowCredentials()` is enabled so the browser can send the `auth_token` cookie cross-origin.

---

## 6. API Endpoints — Complete Reference

All routes use the `ApiResponse<T>` envelope (see [Section 11](#11-api-response-envelope)). Routes marked 🔒 require a valid JWT.

---

### 6.1 Auth — `/api/auth`

#### `POST /api/auth/register`

Creates a new user account and returns a JWT.

**Request body:**
```json
{
  "fullName": "Jane Smith",
  "email": "jane@example.com",
  "password": "SecurePassword123!"
}
```

**Success response (200):**
```json
{
  "success": true,
  "message": "Registration successful!",
  "statusCode": 201,
  "data": {
    "success": true,
    "token": "<JWT string>",
    "userId": "3fa85f64-...",
    "fullName": "Jane Smith",
    "email": "jane@example.com"
  }
}
```

**Error response (400):** Returned when the email is already registered.

---

#### `POST /api/auth/login`

Authenticates an existing user and returns a JWT.

**Request body:**
```json
{
  "email": "jane@example.com",
  "password": "SecurePassword123!"
}
```

**Success response (200):** Same shape as register — returns a fresh JWT.

**Error response (401):** Returned when credentials are invalid or account is deactivated.

---

#### `POST /api/auth/logout`

A stateless endpoint. The actual session termination is handled on the frontend by deleting the `auth_token` cookie. This endpoint simply returns a success confirmation.

---

### 6.2 Users — `/api/users` 🔒

All routes require authentication.

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/users` | Create a user record (admin use) |
| `GET` | `/api/users` | List all users |
| `GET` | `/api/users/{id}` | Get a single user by GUID |
| `PUT` | `/api/users/{id}` | Update user profile fields |
| `DELETE` | `/api/users/{id}` | Soft-delete a user account |

---

### 6.3 Meetings — `/api/meetingcontrollers` 🔒

#### `POST /api/meetingcontrollers`

Creates a new meeting record. The `CreatorId` is automatically extracted from the JWT — you do **not** pass it in the request body.

**Request body:**
```json
{
  "title": "Q3 Roadmap Review",
  "description": "Planning session for Q3",
  "meetingUrl": "https://zoom.us/j/123456789",
  "scheduledAt": "2025-07-15T14:00:00Z"
}
```

---

#### `GET /api/meetingcontrollers`

Returns every meeting in the database (for admin dashboards). Soft-deleted meetings are excluded automatically by the EF query filter.

---

#### `GET /api/meetingcontrollers/{meetingid}`

Returns details for a single meeting by GUID, including its transcript, minutes, participants, and audio files.

---

#### `GET /api/meetingcontrollers/my-meetings`

Returns only the meetings created by the currently logged-in user (extracted from the JWT).

---

#### `PUT /api/meetingcontrollers`

Updates fields on an existing meeting. Ownership is validated — only the `CreatorId` can update.

**Query parameter:** `meetingId=<guid>`

---

#### `DELETE /api/meetingcontrollers/{id}`

Performs a soft-delete by setting `Meeting.IsDeleted = true`. The record is never physically removed. Ownership is validated.

---

#### `POST /api/meetingcontrollers/start`

Manually dispatches a Recall.ai bot to join a live meeting immediately.

**Request body:**
```json
{
  "meetingUrl": "https://zoom.us/j/123456789",
  "internalMeetingId": "3fa85f64-..."
}
```

The bot joins with the name **"MeetingMind"** and is configured with Recall.ai's streaming transcript provider. The bot's ID is returned to the frontend.

**Success response:**
```json
{
  "botId": "bot_abc123",
  "meetingId": "3fa85f64-...",
  "message": "Bot is joining the meeting successfully."
}
```

> **Note:** Scheduled meetings are handled automatically by the `MeetingSchedulerWorker`. This endpoint is for on-demand/immediate recording.

---

### 6.4 Audio — `/api/audiocontrollers` 🔒

#### `POST /api/audiocontrollers/upload`

Direct multipart file upload. The file is received by the server, validated, and stored in Backblaze B2.

**Query parameter:** `meetingId=<guid>`  
**Form field:** `file` (multipart/form-data)

> Upload size is **unlimited** — Kestrel's `MaxRequestBodySize` is explicitly set to `null`.

---

#### `GET /api/audiocontrollers/presigned-url`

Generates a time-limited pre-signed URL so the **frontend browser can upload directly to Backblaze B2**, bypassing the server. This is the preferred approach for large files (avoids proxying through the API).

**Query parameters:** `fileName`, `meetingId`, `contentType`

**How it works:**
1. API generates a unique file path: `audio/{meetingId}/{guid}_{safeFileName}.ext`
2. AWS SDK generates a PUT pre-signed URL valid for **15 minutes**.
3. Frontend uploads the file directly to that URL.
4. Frontend calls `POST /api/audiocontrollers/confirm-upload` to register the file in the database.

---

#### `POST /api/audiocontrollers/confirm-upload`

After a direct-to-B2 upload, the frontend calls this to create the `AudioFile` database record.

**Request body:**
```json
{
  "meetingId": "3fa85f64-...",
  "fileName": "meeting_recording.mp4",
  "filePath": "audio/3fa85f64-.../guid_meeting_recording.mp4",
  "fileUrl": "https://f004.backblazeb2.com/file/minutesassistant/audio/…",
  "fileSize": 52428800,
  "contentType": "audio/mp4"
}
```

The record is created with `Status = pending`, which the `TranscriptionWorker` will detect within 5 seconds and process automatically.

---

#### `GET /api/audiocontrollers/meeting/{meetingId}`

Returns the `AudioFile` record associated with a specific meeting.

---

#### `GET /api/audiocontrollers/user/{id}`

Returns all `AudioFile` records belonging to the currently authenticated user.

---

#### `GET /api/audiocontrollers`

Returns all audio files in the database.

---

### 6.5 Transcription — `/api/transcription`

#### `POST /api/transcription/{id}/transcribe`

Manually triggers transcription for a specific `AudioFile` by its GUID. Normally transcription runs automatically via the `TranscriptionWorker`, but this endpoint allows re-triggering or manual workflows.

**Flow:**
1. Looks up the `AudioFile` record by `id`.
2. Sets `Status = prossesing`.
3. Downloads the audio stream from Backblaze B2 via `IBackblazeB2Service.DownloadFileAsync`.
4. Sends the stream to AssemblyAI via `ITranscriber.TranscribeFromStreamAsync`.
5. Saves the resulting text as a `Transcript` record linked to the meeting.
6. Sets `Status = transcribed`.

---

### 6.6 Minutes — `/api/minutescontrollers` 🔒

#### `POST /api/minutescontrollers/generate`

Manually triggers AI meeting minutes generation for a meeting.

**Request body:**
```json
{
  "meetingId": "3fa85f64-..."
}
```

**Flow:**
1. Verifies the meeting exists.
2. Verifies a `Transcript` record exists for that meeting.
3. Calls `MinutesServices.GenerateMinutesAsync(transcriptId)` → sends transcript to Gemini.
4. Creates a new `Minutes` record in the database with `Status = "draft"` and `LlmModel = "Gemini Pro"`.
5. Returns the raw JSON string produced by Gemini.

> Normally this runs automatically at the end of the `AudioProcessingService` pipeline. Use this endpoint to regenerate minutes manually.

---

#### `GET /api/minutescontrollers/{meetingId}`

Returns the complete meeting data package for a given meeting, including:
- Meeting metadata (title, dates, description, audio URL)
- Parsed and typed minutes (`MeetingMinutesData` object with all structured fields)
- Raw transcript text
- List of participants (name, email, role, join/leave times, duration)

This is the primary endpoint used by the frontend meeting details page.

**The `MeetingMinutesData` JSON structure (as generated by Gemini):**
```json
{
  "meetingTitle": "Q3 Roadmap Review",
  "meetingDate": "2025-07-15",
  "attendees": ["Jane Smith", "John Doe"],
  "absentWithApology": [],
  "absentWithoutApology": [],
  "meetingSummary": "The team reviewed Q3 priorities...",
  "discussionPoints": [
    { "topic": "Budget Allocation", "summary": "Agreed to increase…" }
  ],
  "decisions": ["Increase Q3 budget by 15%"],
  "actionItems": [
    { "owner": "Jane Smith", "task": "Prepare budget breakdown", "dueDate": "2025-07-22" }
  ],
  "nextMeeting": "2025-08-05 at 14:00 UTC"
}
```

---

#### `PUT /api/minutescontrollers/{meetingId}/minutes`

Allows a user to manually edit and save meeting minutes from the frontend.

**Request body:** A `UpdateMinutesRequest` containing the full `MeetingMinutesData` object.

Validates ownership (only the meeting creator can edit). If no `Minutes` record exists, one is created with `LlmModel = "Manual Edit"`. If one already exists, only `DraftJson` and `UpdatedAt` are updated.

---

#### `GET /api/minutescontrollers/all`

Returns all minutes records for all meetings (admin view).

---

### 6.7 Participants — `/api/participantcontrollers` 🔒

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/participantcontrollers/meeting/{meetingId}` | Get all participants for a meeting |
| `POST` | `/api/participantcontrollers/meeting/{meetingId}` | Manually add a participant |
| `PUT` | `/api/participantcontrollers/{participantId}` | Update a participant record |
| `DELETE` | `/api/participantcontrollers/{participantId}` | Remove a participant |

> In normal usage, participants are populated automatically by the webhook handler when Recall.ai fires `participant_events.done`.

---

### 6.8 Recall.ai Webhook — `/api/webhook/recall`

#### `POST /api/webhook/recall`

This is the most complex endpoint. It is the entry point for **all automated processing** after a meeting is recorded.

**Security:** The HMAC-SHA256 signature is verified before any processing begins. Invalid signatures return `401`.

**Event routing:**

| Event | What happens |
|-------|-------------|
| `participant_events.done` | Fetches participant events from Recall.ai API, maps them to `Participant` records, bulk-saves to DB. Runs in a **fire-and-forget background task** to avoid blocking the webhook response. |
| `recording.done` | Extracts the recording ID from the payload (checks `bot.outputs.recordings[0].id`, `data.recording.id`, and `data.outputs.recording.id`). Starts a **background task** that: (1) retries up to 3 times with 10-second delays to get the media URL from Recall.ai, (2) downloads the audio/video stream, (3) uploads it to Backblaze B2, (4) creates an `AudioFile` record with `Status = pending`. The `TranscriptionWorker` will automatically pick this up. |
| `bot.call_ended` | Sets `Meeting.EndedAt = DateTime.UtcNow` and saves. |
| `bot.done` | Sets `Meeting.Status = Completed`. Sends a SignalR push notification to the meeting creator informing them that the recording is saved and ready. |

**Important:** The webhook endpoint always returns `200 OK { received: true }` immediately after routing the event, regardless of whether background processing has completed. This is required because Recall.ai will retry the webhook if it doesn't receive a `2xx` within a timeout window.

---

### 6.9 Health Check — `/health`

Returns `200 Healthy` when the application is running correctly. Used by container orchestrators (Docker, Kubernetes) for liveness probes.

---

## 7. Background Workers

Two `IHostedService` background workers run concurrently with the main API process.

### 7.1 `TranscriptionWorker`

**Purpose:** Automatically processes any `AudioFile` record whose `Status = pending`.

**Polling interval:** Every **5 seconds**.

**Step-by-step flow:**

```
Every 5 seconds:
  1. Query AudioFiles WHERE Status = 'pending' (FIFO — takes first)
  2. If none found → sleep 5 seconds
  3. If found:
     a. Send SignalR notification → "Processing audio: {filename}" (status: "prossesing")
     b. Call AudioProcessingService.ProcessAudioAsync(audioFile.Id)
        i.   Set AudioFile.Status = prossesing
        ii.  Download audio stream from Backblaze B2
        iii. Send stream to AssemblyAI → poll every 3 seconds for up to 3 minutes
        iv.  Save returned text as Transcript record
        v.   Build Gemini prompt with full transcript text (truncated to 100,000 chars if needed)
        vi.  Call Gemini 2.5 Flash API → receive JSON minutes
        vii. Validate/repair the JSON (close unclosed brackets/braces if truncated)
        viii.Save Minutes record to database
        ix.  Set AudioFile.Status = transcribed
        x.   Set Meeting.Status = Completed
        xi.  Send SignalR notification → "Your meeting minutes are ready!" (status: "Completed")
     c. On success → send "transcribed" notification
     d. On failure → send "failed" notification with error message
```

**Rate limit handling:** If Gemini or Groq returns a `429 Too Many Requests`, the audio file is reverted to `Status = pending` so it will be retried on the next poll cycle.

---

### 7.2 `MeetingSchedulerWorker`

**Purpose:** Automatically deploys a Recall.ai recording bot for any meeting that is approaching its scheduled start time.

**Polling interval:** Every **10 seconds**.

**Deployment window:** The bot is deployed **1 minute before** `Meeting.ScheduledAt`.

**Look-ahead window:** Only meetings within the next **24 hours** are considered.

**Stale guard:** Meetings that started more than **5 minutes ago** without a bot are skipped (to avoid deploying a bot to a meeting that's already over).

**Criteria for deployment:**
- `ScheduledAt` is not null
- `ScheduledAt` is within the [now - 5 min, now + 24 hr] window
- `Status = Scheduled`
- `RecallBotId IS NULL` (not already deployed)
- `MeetingUrl` is not empty
- `IsDeleted = false`

**Circuit breaker:** If 5 consecutive bot deployments fail, the worker enters a 5-minute pause before retrying to avoid hammering the Recall.ai API.

**Retry policy (per deployment):** Polly `WaitAndRetryAsync` — up to 3 retries with exponential backoff (2s, 4s, 8s) on `HttpRequestException` or `TaskCanceledException`.

---

## 8. External Service Integrations

### 8.1 Recall.ai

Recall.ai is a service that can join any Zoom, Google Meet, or Microsoft Teams meeting as a bot participant. It records the meeting and fires webhook events as it progresses.

**How this project uses it:**

1. **Create a bot** (`POST /api/v1/bot`) — Sends the meeting URL, bot name, and transcript configuration. Returns a `botId`.
2. **Get recording media** (`GET /api/v1/recording/{id}/media`) — After `recording.done`, fetches the signed download URL for the audio/video file.
3. **Get participant events** (`GET /api/v1/recording/{id}/participant-events`) — After `participant_events.done`, fetches the join/leave timeline for each attendee.
4. **Download recording** — Streams the audio/video from Recall.ai's pre-signed URL directly into memory, then re-uploads to Backblaze B2.

**Bot configuration:**
```json
{
  "bot_name": "MeetingMind",
  "meeting_url": "...",
  "recording_config": {
    "transcript": {
      "provider": {
        "recall_ai_streaming": {}
      }
    }
  }
}
```

**Webhook events handled:** `participant_events.done`, `recording.done`, `bot.call_ended`, `bot.done`

---

### 8.2 AssemblyAI

AssemblyAI is the speech-to-text service that converts recorded meeting audio into a readable transcript.

**Why AssemblyAI instead of running Whisper locally?**  
The Whisper.net package is included in the project dependencies for potential local use, but AssemblyAI is used for production because it handles large audio files reliably in the cloud without requiring GPU resources.

**The three-step process:**

**Step 1 — Upload audio to AssemblyAI:**
```
POST https://api.assemblyai.com/v2/upload
Authorization: <API_KEY>
Content-Type: application/octet-stream
Body: <raw audio stream>
→ Returns { upload_url: "https://..." }
```

**Step 2 — Submit transcription job:**
```
POST https://api.assemblyai.com/v2/transcript
Authorization: <API_KEY>
Body: { audio_url: "<upload_url>" }
→ Returns { id: "transcript_abc123", status: "queued" }
```

**Step 3 — Poll for completion (every 3 seconds, max 60 attempts = 3 minutes):**
```
GET https://api.assemblyai.com/v2/transcript/{id}
Authorization: <API_KEY>
→ Returns { status: "queued" | "processing" | "completed" | "error", text: "..." }
```

When `status = "completed"`, the `text` field contains the full transcript.

---

### 8.3 Google Gemini (Minutes Generation)

Google Gemini 2.5 Flash is used to transform a raw meeting transcript into structured, professional meeting minutes.

**Primary model:** `gemini-2.5-flash`  
**Fallback model:** `gemini-1.5-flash` (used if the primary returns 503/429 errors after all retries)

**Prompt engineering:** The system sends a carefully crafted prompt that:
- Defines Gemini's role as "an expert meeting secretary with 20 years of experience"
- Provides strict rules (no hallucination, JSON only, specific owners for action items)
- Provides the exact JSON output schema
- Appends the full transcript text

**Generation parameters:**
```json
{
  "temperature": 0.3,
  "topP": 0.8,
  "topK": 40,
  "maxOutputTokens": 65536
}
```

**Transcript size guard:** If the transcript exceeds 100,000 characters, it is truncated and `[TRANSCRIPT TRUNCATED DUE TO LENGTH]` is appended.

**JSON cleaning pipeline:**
1. Strip markdown code fences (` ```json ` prefix and ` ``` ` suffix)
2. Remove trailing commas before `}` or `]`
3. **Repair truncated JSON:** If the token limit is hit and the response is cut off mid-JSON, the cleaner counts unmatched brackets/braces and closes them automatically.

**Retry policy (Polly):** Up to 5 retries with exponential backoff + random jitter on `503 Service Unavailable` and `429 Too Many Requests`.

---

### 8.4 Backblaze B2 (Storage)

Two separate clients are used for different B2 operations:

#### `BackblazeStorage` (S3-compatible, via AWSSDK.S3)

Used for **uploading** files. The AWS S3 SDK works with B2's S3-compatible API.

- `UploadAsync(IFormFile, folder)` — Direct multipart upload from an HTTP form.
- `UploadStreamAsync(Stream, fileName, contentType, folder)` — Streaming upload (used by the webhook handler after downloading from Recall.ai).
- `GetPresignedUploadUrlAsync(key, contentType, minutes)` — Generates a PUT pre-signed URL so the frontend can upload directly.
- `GetFileUrl(key)` — Returns `https://f004.backblazeb2.com/file/{bucket}/{key}`.
- `DeleteAsync(fileId)` — Deletes a file.

#### `BackblazeB2Service` (native B2 API, via raw HTTP)

Used for **downloading** and **generating signed download URLs**. The S3 SDK doesn't support B2's native download authorization, so the native B2 API is used here.

- `EnsureAuthorizedAsync()` — Calls `b2_authorize_account` to get an auth token. Tokens are cached for 23 hours.
- `DownloadFileAsync(b2FilePath)` — Downloads a file as a `Stream`. Used by `AudioProcessingService` to stream audio to AssemblyAI without loading it entirely into memory.
- `GetSignedUrlAsync(b2FilePath, validDurationSeconds)` — Calls `b2_get_download_authorization` to generate a time-limited download URL.

---

## 9. Real-Time Notifications (SignalR)

The `NotificationHub` at `/hubs/notifications` provides real-time push notifications to the frontend.

### Connection

The frontend connects with the JWT passed as a query string:
```
wss://api.yourdomain.com/hubs/notifications?access_token=<JWT>
```

### User Grouping

When a user connects, `OnConnectedAsync` adds their `connectionId` to a SignalR group named after their `UserId` (GUID string). This means any server-side code can push a message to a specific user, even if they have multiple browser tabs open, using:

```csharp
_hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveTranscriptionUpdate", payload)
```

### Events pushed to the frontend

| Event Name | When fired | Payload |
|-----------|-----------|---------|
| `ReceiveTranscriptionUpdate` | Audio starts processing | `{ meetingId, status: "prossesing", message: "Processing audio: …" }` |
| `ReceiveTranscriptionUpdate` | Transcription + minutes complete | `{ meetingId, audioId, status: "Completed", message: "Your meeting minutes are ready!" }` |
| `ReceiveTranscriptionUpdate` | Transcription failed | `{ meetingId, status: "failed", message: "Transcription failed: …" }` |
| `ReceiveTranscriptionUpdate` | Recall.ai bot.done | `{ meetingId, meetingtitle, status: "Completed", message: "Recording saved and ready for processing!" }` |

---

## 10. The Automated Pipeline — End to End

This is what happens after a user creates and schedules a meeting:

```
1. USER creates a meeting via POST /api/meetingcontrollers
   └─ Database: Meeting { Status: Created, ScheduledAt: "2025-07-15T14:00Z" }

2. USER updates meeting URL via PUT /api/meetingcontrollers
   └─ Database: Meeting { Status: Scheduled, MeetingUrl: "https://zoom.us/j/123" }

3. MeetingSchedulerWorker (polling every 10s)
   └─ At 13:59 UTC: detects meeting starts in <= 1 minute
   └─ Calls Recall.ai POST /api/v1/bot → gets botId
   └─ Database: Meeting { Status: Recording, RecallBotId: "bot_xyz" }

4. Recall.ai BOT joins the Zoom/Meet/Teams meeting
   └─ Records audio + video

5. Meeting ends → Recall.ai fires webhooks to POST /api/webhook/recall:

   a. participant_events.done
      └─ Background: GET recall.ai/recording/{id}/participant-events
      └─ Database: Participants table populated with join/leave times

   b. recording.done
      └─ Background:
         i.   GET recall.ai/recording/{id}/media → get signed download URL
         ii.  Stream download audio from Recall.ai
         iii. Stream upload to Backblaze B2 → key: audio/{meetingId}/{guid}_recording.mp4
         iv.  Database: AudioFile { Status: pending, FilePath: "audio/…", FileUrl: "https://…" }
         v.   Database: Meeting { AudioUrl: "https://…" }

   c. bot.call_ended
      └─ Database: Meeting { EndedAt: now }

   d. bot.done
      └─ Database: Meeting { Status: Completed }
      └─ SignalR → push to user: "Recording saved and ready for processing!"

6. TranscriptionWorker (polling every 5s)
   └─ Finds AudioFile { Status: pending }
   └─ SignalR → push: "Processing audio…"
   └─ Calls AudioProcessingService.ProcessAudioAsync:

      a. TRANSCRIPTION:
         i.   Download audio stream from Backblaze B2
         ii.  Upload stream to AssemblyAI
         iii. Poll AssemblyAI every 3s (up to 3 minutes)
         iv.  Database: Transcript { RawText: "Hello everyone, today we…" }

      b. MINUTES GENERATION:
         i.   Build prompt with transcript text
         ii.  Call Google Gemini 2.5 Flash API
         iii. Clean & validate/repair returned JSON
         iv.  Database: Minutes { DraftJson: '{"meetingTitle":…}', LlmModel: "Gemini Pro" }

      c. COMPLETION:
         i.   Database: AudioFile { Status: transcribed }
         ii.  Database: Meeting { Status: Completed }
         iii. SignalR → push to user: "Your meeting minutes are ready to view!"

7. USER opens the frontend → sees notification → clicks to view minutes
   └─ GET /api/minutescontrollers/{meetingId}
   └─ Returns meeting + parsed minutes + transcript + participants
```

---

## 11. API Response Envelope

Every API endpoint returns a consistent `ApiResponse<T>` wrapper:

```json
{
  "success": true,
  "message": "Human-readable description",
  "statusCode": 200,
  "data": { ... }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `success` | `bool` | `true` for successful calls, `false` for failures |
| `message` | `string` | Human-readable description of the result |
| `statusCode` | `int` | HTTP status code mirrored in the body |
| `data` | `T?` | The actual response payload; `null` on failure |

**Failure example:**
```json
{
  "success": false,
  "message": "Meeting not found.",
  "statusCode": 404,
  "data": null
}
```

---

## 12. Configuration Reference

All configuration is in `appsettings.json`. **Replace every placeholder value before deploying.**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=<supabase-host>;Database=postgres;Username=<user>;******;SSL Mode=Require;Trust Server Certificate=true"
  },

  "JwtSettings": {
    "SecretKey": "<min-32-char-random-string>",
    "Issuer": "MeetingAssistantAPI",
    "Audience": "MeetingAssistantClient",
    "ExpiryInMinutes": 60
  },

  "BackblazeB2": {
    "ApplicationKeyId": "<your-key-id>",
    "ApplicationKey": "<your-application-key>",
    "BucketId": "<your-bucket-id>",
    "BucketName": "<your-bucket-name>",
    "Endpoint": "https://s3.eu-central-003.backblazeb2.com"
  },

  "RecallAi": {
    "ApiKey": "<your-recall-ai-api-key>",
    "BaseUrl": "https://eu-central-1.recall.ai/api/v1/",
    "WebhookSecret": "whsec_<your-webhook-signing-secret>"
  },

  "AssemblyAI": {
    "ApiKey": "<your-assemblyai-api-key>"
  },

  "GeminiApi": {
    "ApiKey": "<your-gemini-api-key>",
    "Url": "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent",
    "Model": "gemini-2.5-flash",
    "FallbackUrl": "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent",
    "FallbackModel": "gemini-1.5-flash"
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

### Recall.ai Webhook Setup

In the Recall.ai dashboard, register the following webhook URL:

```
POST https://<your-deployed-domain>/api/webhook/recall
```

Enable these event types:
- `participant_events.done`
- `recording.done`
- `bot.call_ended`
- `bot.done`

Copy the **Signing Secret** from the dashboard and set it as `RecallAi:WebhookSecret` in your configuration (must start with `whsec_`).

---

## 13. Running Locally

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL (or a free [Supabase](https://supabase.com) project)
- (Optional) [ngrok](https://ngrok.com) to expose your local API to Recall.ai webhooks

### Steps

```bash
# 1. Clone the repository
git clone https://github.com/mainavict/finalyearproject.git
cd finalyearproject

# 2. Fill in your API keys in appsettings.Development.json
#    (ConnectionStrings, JwtSettings, BackblazeB2, RecallAi, AssemblyAI, GeminiApi)

# 3. Apply database migrations
dotnet ef database update

# 4. Run the API
dotnet run

# API starts at: https://localhost:7xxx (check the console output)
# Interactive docs: https://localhost:7xxx/scalar/v1
# Health check:     https://localhost:7xxx/health
```

### Exposing Webhooks Locally (for Recall.ai)

```bash
# Install ngrok and expose your local port
ngrok http https://localhost:7xxx

# Register the ngrok URL in Recall.ai dashboard:
# https://abc123.ngrok-free.app/api/webhook/recall
```

---

## 14. Deploying with Docker

A `Dockerfile` is included in the project root.

```bash
# Build the Docker image
docker build -t meeting-assistant-api .

# Run the container (pass config via environment variables or mounted appsettings)
docker run -d \
  -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=..." \
  -e JwtSettings__SecretKey="..." \
  -e RecallAi__ApiKey="..." \
  -e RecallAi__WebhookSecret="whsec_..." \
  -e AssemblyAI__ApiKey="..." \
  -e GeminiApi__ApiKey="..." \
  -e BackblazeB2__ApplicationKeyId="..." \
  -e BackblazeB2__ApplicationKey="..." \
  -e BackblazeB2__BucketId="..." \
  -e BackblazeB2__BucketName="..." \
  meeting-assistant-api
```

The health check endpoint `/health` can be used as the Docker/Kubernetes liveness probe:

```dockerfile
HEALTHCHECK --interval=30s --timeout=5s CMD curl -f http://localhost:8080/health || exit 1
```
