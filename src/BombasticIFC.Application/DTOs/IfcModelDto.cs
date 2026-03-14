using BombasticIFC.Domain.Enums;

namespace BombasticIFC.Application.DTOs;

/// <summary>
/// Data transfer object for IFC model
/// </summary>
public class IfcModelDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public ModelStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ModelMetadataDto? Metadata { get; set; }
    /// <summary>Relative URL to download the converted XKT file. Null until a conversion job completes.</summary>
    public string? XktOutputUrl { get; set; }
    /// <summary>Relative URL to download the original IFC file. Always set once the model exists.</summary>
    public string? OriginalFileUrl { get; set; }
}

public class ModelMetadataDto
{
    public string IfcSchema { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public int NumberOfElements { get; set; }
    public Dictionary<string, int> ElementTypeCounts { get; set; } = new();
}
