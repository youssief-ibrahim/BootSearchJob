
using DotNetJobHunter.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetJobHunter.Data;

public class JobDbContext : DbContext
{
    public JobDbContext(
        DbContextOptions<JobDbContext> options)
        : base(options)
    {
    }

    public DbSet<JobPosting> Jobs => Set<JobPosting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<JobPosting>()
            .HasIndex(j => j.Url)
            .IsUnique();

        modelBuilder.Entity<JobPosting>()
            .Property(j => j.Title)
            .HasMaxLength(300);

        modelBuilder.Entity<JobPosting>()
            .Property(j => j.Url)
            .HasMaxLength(2048);

        modelBuilder.Entity<JobPosting>()
            .Property(j => j.Source)
            .HasMaxLength(100);
    }
}
