using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KoreForge.Scripts.Data.Entities;

[Table("Scripts", Schema = "dbo")]
public class ScriptEntity
{
    [Key]
    public long ScriptId { get; set; }

    [Required, MaxLength(200)]
    public string ApplicationId { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string TypeTag { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Language { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    [Required, MaxLength(100)]
    public string ModifiedBy { get; set; } = string.Empty;

    public DateTime ModifiedDate { get; set; }

    [MaxLength(4000)]
    public string? Comment { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
