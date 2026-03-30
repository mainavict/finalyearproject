using Microsoft.EntityFrameworkCore;
using finalyearproject.Models;

namespace finalyearproject.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
}