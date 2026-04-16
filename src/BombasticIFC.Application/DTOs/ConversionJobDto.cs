using BombasticIFC.Domain.Enums;

namespace BombasticIFC.Application.DTOs;

/// <summary>
/// Data transfer object for conversion job
/// </summary>
public class ConversionJobDto
{
    public Guid Id { get; set; }
    public Guid ModelId { get; set; }
    public ConversionFormat TargetFormat { get; set; }
    public ConversionStatus Status { get; set; }
    public int ProgressPercentage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool HasOutput { get; set; }
    public string? ErrorMessage { get; set; }
}
