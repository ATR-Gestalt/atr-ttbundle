using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StlTechRelGen.Db;

[Table("mods")]
public partial class Mod {
	[Key]
	[Column("id", TypeName = "char(36)")]
	public string Id { get; set; } = null!;

	[Column("displayName", TypeName = "varchar(255)")]
	public string? DisplayName { get; set; }

	[Column("dirPath")]
	public string? DirPath { get; set; }

	[Column("archivePath")]
	public string? ArchivePath { get; set; }

	[Column("status")]
	public string Status { get; set; } = null!;
}
