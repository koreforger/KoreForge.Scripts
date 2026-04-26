using KF.Scripts.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace KF.Scripts.Data;

public class KFScriptsDbContext : DbContext
{
    public DbSet<ScriptEntity> Scripts => Set<ScriptEntity>();
    public DbSet<ScriptHistoryEntity> ScriptHistory => Set<ScriptHistoryEntity>();

    public KFScriptsDbContext(DbContextOptions<KFScriptsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScriptEntity>()
            .HasIndex(x => new { x.ApplicationId, x.Name })
            .HasDatabaseName("UX_Scripts_App_Name")
            .IsUnique();

        modelBuilder.Entity<ScriptEntity>()
            .HasIndex(x => new { x.ApplicationId, x.TypeTag })
            .HasDatabaseName("IX_Scripts_App_TypeTag");

        modelBuilder.Entity<ScriptHistoryEntity>()
            .HasIndex(x => x.ScriptId)
            .HasDatabaseName("IX_ScriptHistory_ScriptId");

        modelBuilder.Entity<ScriptHistoryEntity>()
            .HasIndex(x => new { x.ApplicationId, x.Name, x.ChangedDate })
            .HasDatabaseName("IX_ScriptHistory_AppNameDate");
    }
}
