using Microsoft.EntityFrameworkCore;
using finalyearproject.Models;

namespace finalyearproject.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public  DbSet<Meeting> Meetings { get; set; }
    
    public DbSet<AudioFile> AudioFiles { get; set; }
    
    public DbSet<Transcript> Transcripts { get; set; }
    
    public DbSet<Minutes> Minutes { get; set; }
    
    
    
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        
        b.Entity<AudioFile>()
            .Property(a=>a.Status)
            .HasConversion<string>();

        b.Entity<Meeting>()
            .Property(m => m.Status)
            .HasConversion<string>();
        
        b.Entity<EmailOtp>()
            .Property(o => o.Purpose)
            .HasConversion<string>();
        
        b.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
        b.Entity<Meeting>()
            .HasQueryFilter(m => !m.IsDeleted);
        

        b.Entity<Meeting>()
            .Property(m => m.Status)
            .HasConversion<string>();


        b.Entity<Minutes>()
            .HasGeneratedTsVectorColumn(
                m => m.SearchVector,
                "english",
                m => new { m.ApprovedHtml })
            .HasIndex(m => m.SearchVector)
            .HasMethod("GIN");


        b.Entity<Transcript>()
            .HasIndex(t => t.MeetingId)
            .IsUnique();


        b.Entity<MinutesVersion>()
            .HasIndex(v => new { v.MinutesId, v.VersionNumber });


        b.Entity<EmailDeliveryLog>()
            .HasIndex(d => d.SendGridMessageId);

        b.Entity<EmailDeliveryLog>()
            .HasIndex(d => d.MeetingId);
    }

}