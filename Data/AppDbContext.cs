using Microsoft.EntityFrameworkCore;
using finalyearproject.Models;

namespace finalyearproject.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    
    
    
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Organization>()
            .Property(o => o.Plan)
            .HasConversion<int>();


        b.Entity<Meeting>()
            .HasQueryFilter(m => !m.IsDeleted);

        b.Entity<Meeting>()
            .HasIndex(m => m.OrgId);

        b.Entity<Meeting>()
            .HasIndex(m => new { m.OrgId, m.Status });

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