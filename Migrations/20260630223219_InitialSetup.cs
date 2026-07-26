using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace finalyearproject.Migrations
{
    /// <inheritdoc />
    public partial class InitialSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "\"SubscriptionPlans\"",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanCode = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    PriceKSH = table.Column<int>(type: "integer", nullable: false),
                    IncludedMinutes = table.Column<int>(type: "integer", nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_\"SubscriptionPlans\"", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailOtps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOtps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ClerkUserId = table.Column<string>(type: "text", nullable: true),
                    SubscriptionTier = table.Column<string>(type: "text", nullable: false),
                    SubscriptionExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MonthlyMinuteLimit = table.Column<int>(type: "integer", nullable: false),
                    MinutesUsedThisMonth = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "\"Payments\"",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CheckoutRequestID = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanType = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_\"Payments\"", x => x.Id);
                    table.ForeignKey(
                        name: "FK_\"Payments\"_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MeetingUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AudioUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AudioDurationSecs = table.Column<int>(type: "integer", nullable: true),
                    AudioMimeType = table.Column<string>(type: "text", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RecallBotId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meetings_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AudioFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioFiles_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailDeliveryLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SendGridMessageId = table.Column<string>(type: "text", nullable: false),
                    RecipientEmail = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    EventAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BounceReason = table.Column<string>(type: "text", nullable: true),
                    RawPayload = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailDeliveryLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailDeliveryLogs_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Minutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    DraftJson = table.Column<string>(type: "text", maxLength: 100000000, nullable: false),
                    ApprovedHtml = table.Column<string>(type: "text", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LlmModel = table.Column<string>(type: "text", nullable: true),
                    PromptTokens = table.Column<int>(type: "integer", nullable: true),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: true),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true)
                        .Annotation("Npgsql:TsVectorConfig", "english")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "ApprovedHtml" }),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Minutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Minutes_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Minutes_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsHost = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeftAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Participants_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transcripts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawText = table.Column<string>(type: "text", nullable: false),
                    Segments = table.Column<string>(type: "text", nullable: true),
                    WhisperModel = table.Column<string>(type: "text", nullable: true),
                    DurationSecs = table.Column<int>(type: "integer", nullable: true),
                    WordCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transcripts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transcripts_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MinutesVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MinutesId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    DraftJson = table.Column<string>(type: "text", nullable: false),
                    EditSummary = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinutesVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinutesVersions_Minutes_MinutesId",
                        column: x => x.MinutesId,
                        principalTable: "Minutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MinutesVersions_Users_EditorUserId",
                        column: x => x.EditorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "\"SubscriptionPlans\"",
                columns: new[] { "Id", "CreatedAt", "Description", "DisplayName", "DurationDays", "IncludedMinutes", "IsActive", "PlanCode", "PriceKSH" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 6, 30, 22, 32, 18, 629, DateTimeKind.Utc).AddTicks(4331), "10 minutes to get started", "Free Trial", 30, 10, true, "trial", 0 },
                    { 2, new DateTime(2026, 6, 30, 22, 32, 18, 629, DateTimeKind.Utc).AddTicks(6125), "Instant top-up: 30 minutes", "Pay-As-You-Go", 0, 30, true, "payg_50", 50 },
                    { 3, new DateTime(2026, 6, 30, 22, 32, 18, 629, DateTimeKind.Utc).AddTicks(6128), "200 minutes per month", "Pro Plan", 30, 200, true, "pro", 300 },
                    { 4, new DateTime(2026, 6, 30, 22, 32, 18, 629, DateTimeKind.Utc).AddTicks(6130), "600 minutes per month", "Ultra Pro", 30, 600, true, "ultra_pro", 800 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_\"Payments\"_CheckoutRequestID",
                table: "\"Payments\"",
                column: "CheckoutRequestID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_\"Payments\"_UserId",
                table: "\"Payments\"",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_\"SubscriptionPlans\"_PlanCode",
                table: "\"SubscriptionPlans\"",
                column: "PlanCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AudioFiles_MeetingId",
                table: "AudioFiles",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveryLogs_MeetingId",
                table: "EmailDeliveryLogs",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveryLogs_SendGridMessageId",
                table: "EmailDeliveryLogs",
                column: "SendGridMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_CreatorId",
                table: "Meetings",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_CreatorId_Status",
                table: "Meetings",
                columns: new[] { "CreatorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Minutes_ApproverUserId",
                table: "Minutes",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Minutes_MeetingId",
                table: "Minutes",
                column: "MeetingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Minutes_SearchVector",
                table: "Minutes",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_MinutesVersions_EditorUserId",
                table: "MinutesVersions",
                column: "EditorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MinutesVersions_MinutesId_VersionNumber",
                table: "MinutesVersions",
                columns: new[] { "MinutesId", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Participants_Email",
                table: "Participants",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_MeetingId_Email",
                table: "Participants",
                columns: new[] { "MeetingId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_Transcripts_MeetingId",
                table: "Transcripts",
                column: "MeetingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "\"Payments\"");

            migrationBuilder.DropTable(
                name: "\"SubscriptionPlans\"");

            migrationBuilder.DropTable(
                name: "AudioFiles");

            migrationBuilder.DropTable(
                name: "EmailDeliveryLogs");

            migrationBuilder.DropTable(
                name: "EmailOtps");

            migrationBuilder.DropTable(
                name: "MinutesVersions");

            migrationBuilder.DropTable(
                name: "Participants");

            migrationBuilder.DropTable(
                name: "Transcripts");

            migrationBuilder.DropTable(
                name: "Minutes");

            migrationBuilder.DropTable(
                name: "Meetings");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
