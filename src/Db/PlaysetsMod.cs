using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace StlTechRelGen.Db;

[Keyless]
[Table("playsets_mods")]
public partial class PlaysetsMod {
	[Column("playsetId", TypeName = "char(36)")]
	public string PlaysetId { get; set; } = null!;

	[Column("modId", TypeName = "char(36)")]
	public string ModId { get; set; } = null!;

	[Column("enabled", TypeName = "boolean")]
	public bool? Enabled { get; set; }

	[Column("position")]
	public int? Position { get; set; }
}
