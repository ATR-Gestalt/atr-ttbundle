using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StlTechRelGen.Db;

[Table("playsets")]
public partial class Playset {
	[Key]
	[Column("id", TypeName = "char(36)")]
	public string Id { get; set; } = null!;

	[Column("name", TypeName = "varchar(255)")]
	public string Name { get; set; } = null!;

	[Column("isActive", TypeName = "boolean")]
	public bool? IsActive { get; set; }
}
