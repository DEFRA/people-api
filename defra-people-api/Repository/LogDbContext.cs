using Defra_People_API.Entities;
using Microsoft.EntityFrameworkCore;

namespace Defra_People_API.Repository;

public class LogDbContext : DbContext
{
    public LogDbContext(DbContextOptions<LogDbContext> options) : base(options)
    {
    }

    public DbSet<LogItem> ApiLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LogItem>(entity =>
        {
            entity.ToTable("PeopleServiceLog");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Consumer).IsRequired();
            entity.Property(e => e.Endpoint).IsRequired();
            entity.Property(e => e.User).IsRequired();
            entity.Property(e => e.Params).IsRequired();
            entity.Property(e => e.Response).IsRequired();
            entity.Property(e => e.Timestamp).IsRequired();
        });
    }
}
