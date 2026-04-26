using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KF.Scripts.Data.Entities;

[Table("ScriptHistory", Schema = "dbo")]
public class ScriptHistoryEntity
{
    [Key]
    public long HistoryId { get; set; }

    public long ScriptId { get; set; }

    [Required, MaxLength(200)]
    public string ApplicationId { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    public string? OldContent { get; set; }
    public string? NewContent { get; set; }
    public bool? OldIsEnabled { get; set; }
    public bool? NewIsEnabled { get; set; }
    public byte[]? RowVersionBefore { get; set; }
    public byte[]? RowVersionAfter { get; set; }

    [Required, MaxLength(100)]
    public string ChangedBy { get; set; } = string.Empty;

    public DateTime ChangedDate { get; set; }

    [Required, MaxLength(50)]
    public string Operation { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Comment { get; set; }
}
