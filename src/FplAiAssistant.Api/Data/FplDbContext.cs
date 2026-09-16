using FplAiAssistant.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FplAiAssistant.Api.Data;

public class FplDbContext : DbContext
{
    public FplDbContext(DbContextOptions<FplDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players => Set<Player>();

    public DbSet<Team> Teams => Set<Team>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).ValueGeneratedNever();
            entity.Property(p => p.Price).HasPrecision(4, 1);
            entity.Property(p => p.Form).HasPrecision(4, 1);
            entity.Property(p => p.SelectedByPercent).HasPrecision(5, 1);

            entity.HasOne(p => p.Team)
                  .WithMany(t => t.Players)
                  .HasForeignKey(p => p.TeamId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
