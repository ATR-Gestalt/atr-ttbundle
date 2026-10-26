using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace StlTechRelGen.Db;

internal sealed partial class LauncherV2DbContext(
	Config.GameConfig config
) : DbContext {
	public DbSet<Mod> Mods { get; private set; }

	public DbSet<Playset> Playsets { get; private set; }

	public DbSet<PlaysetsMod> PlaysetsMods { get; private set; }

	protected override void OnConfiguring(
		DbContextOptionsBuilder optionsBuilder
	) => optionsBuilder
		.UseSqlite(
			new SqliteConnectionStringBuilder() {
				DataSource = Path.Combine(config.DocumentPath, "launcher-v2.sqlite"),
				Mode = SqliteOpenMode.ReadOnly
			}.ConnectionString
		);

	protected override void OnModelCreating(ModelBuilder modelBuilder) {
		modelBuilder.Entity<PlaysetsMod>(entity =>
			entity.Property(e => e.Enabled).HasDefaultValueSql("'1'")
		);

		OnModelCreatingPartial(modelBuilder);
	}

	partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
