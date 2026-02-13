namespace BombasticIFC.Domain.Entities;

/// <summary>
/// Represents a version of an IFC model
/// </summary>
public class ModelVersion : BaseEntity
{
    public Guid ModelId { get; private set; }
    public string VersionNumber { get; private set; }
    public string Description { get; private set; }
    public string? FilePath { get; private set; }
    public bool IsActive { get; private set; }
    
    // Relationship
    public IfcModel? Model { get; private set; }

    private ModelVersion() 
    {
        VersionNumber = string.Empty;
        Description = string.Empty;
    }

    public static ModelVersion Create(Guid modelId, string versionNumber, string description)
    {
        if (string.IsNullOrWhiteSpace(versionNumber))
            throw new ArgumentException("Version number cannot be empty", nameof(versionNumber));

        return new ModelVersion
        {
            ModelId = modelId,
            VersionNumber = versionNumber,
            Description = description ?? string.Empty,
            IsActive = true
        };
    }

    public void SetFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));

        FilePath = filePath;
        MarkAsUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }
}
