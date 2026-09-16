using Microsoft.EntityFrameworkCore;

namespace Trama.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var project = modelBuilder.Entity<Project>();
        project.ToTable("Projects");
        project.Property(p => p.Name).IsRequired().HasMaxLength(120);
        project.Property(p => p.Description).HasMaxLength(500);
        project.Property(p => p.Target).HasConversion<string>().HasMaxLength(16);
        project.Property(p => p.Units).HasConversion<string>().HasMaxLength(16);
        project.Property(p => p.Kind).HasConversion<string>().HasMaxLength(16);
        project.HasIndex(p => p.UpdatedAt);
        project.HasIndex(p => p.Name);
    }
}
