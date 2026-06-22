using Microsoft.EntityFrameworkCore;
using finalyearproject.Models;
using NpgsqlTypes;

namespace finalyearproject.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    // ── DbSets ────────────────────────────────────────────────────────────
    public DbSet<Participant> Participants { get; set; }
    public DbSet<User>             Users             { get; set; }
    public DbSet<Meeting>          Meetings          { get; set; }
    public DbSet<AudioFile>        AudioFiles        { get; set; }
    public DbSet<Transcript>       Transcripts       { get; set; }
    public DbSet<Minutes>          Minutes           { get; set; }
    public DbSet<MinutesVersion>   MinutesVersions   { get; set; }
  
    public DbSet<EmailDeliveryLog> EmailDeliveryLogs { get; set; }
    public DbSet<EmailOtp>         EmailOtps         { get; set; }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ── USER ─────────────────────────────────────────────────────────
        b.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // ── MEETING ───────────────────────────────────────────────────────
        b.Entity<Meeting>()
            .Property(m => m.Status)
            .HasConversion<string>();

        // Soft delete — only return non-deleted meetings
        b.Entity<Meeting>()
            .HasQueryFilter(m => !m.IsDeleted);

        b.Entity<Meeting>()
            .HasIndex(m => m.CreatorId);

        b.Entity<Meeting>()
            .HasIndex(m => new { m.CreatorId, m.Status });
        
        b.Entity<Participant>(entity =>
        {
            entity.HasOne(p => p.Meeting)
                .WithMany(m => m.Participants)
                .HasForeignKey(p => p.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(p => new { p.MeetingId, p.Email });
            entity.HasIndex(p => p.Email);
        });

        // ── AUDIO FILE ────────────────────────────────────────────────────
        b.Entity<AudioFile>()
            .Property(a => a.Status)
            .HasConversion<string>();

        // ── TRANSCRIPT ────────────────────────────────────────────────────
        // One transcript per meeting — enforce with unique index
        b.Entity<Transcript>()
            .HasIndex(t => t.MeetingId)
            .IsUnique();

        // Fix EF Core warning:
        // Transcript navigates through Meeting which has a query filter
        // We must add a matching filter here
        b.Entity<Transcript>()
            .HasQueryFilter(t => !t.Meeting.IsDeleted);

        // ── MINUTES ───────────────────────────────────────────────────────
        // One minutes record per meeting
        b.Entity<Minutes>()
            .HasIndex(m => m.MeetingId)
            .IsUnique();

        // Fix EF Core warning — match the Meeting query filter
        b.Entity<Minutes>()
            .HasQueryFilter(m => !m.Meeting.IsDeleted);

        // Full-text search index on approved minutes HTML
        b.Entity<Minutes>()
            .HasGeneratedTsVectorColumn(
                m => m.SearchVector,
                "english",
                m => new { m.ApprovedHtml });

        b.Entity<Minutes>()
            .HasIndex(m => m.SearchVector)
            .HasMethod("GIN");

        // ── MINUTES VERSION ───────────────────────────────────────────────
        b.Entity<MinutesVersion>()
            .HasIndex(v => new { v.MinutesId, v.VersionNumber });

        b.Entity<MinutesVersion>()
            .HasQueryFilter(v => !v.Minutes.Meeting.IsDeleted);

        // ── EMAIL DELIVERY LOG ────────────────────────────────────────────
        b.Entity<EmailDeliveryLog>()
            .HasIndex(d => d.SendGridMessageId);

        b.Entity<EmailDeliveryLog>()
            .HasIndex(d => d.MeetingId);

        // Fix EF Core warning — match the Meeting query filter
        b.Entity<EmailDeliveryLog>()
            .HasQueryFilter(e => !e.Meeting.IsDeleted);

        // ── EMAIL OTP ─────────────────────────────────────────────────────
        b.Entity<EmailOtp>()
            .Property(o => o.Purpose)
            .HasConversion<string>();
    }
}