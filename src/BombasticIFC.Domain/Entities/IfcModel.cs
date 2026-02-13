using BombasticIFC.Domain.Enums;
using BombasticIFC.Domain.ValueObjects;

namespace BombasticIFC.Domain.Entities;

/// <summary>
/// Represents an IFC model in the domain
/// </summary>
public class IfcModel : BaseEntity
{
    public string FileName { get; private set; }
    public string OriginalFilePath { get; private set; }
    public long FileSizeBytes { get; private set; }
    public ModelMetadata Metadata { get; private set; }
    public ModelStatus Status { get; private set; }
    public Guid? UserId { get; private set; }
    
    // Relationships
    public ICollection<ConversionJob> ConversionJobs { get; private set; }
    public ICollection<ModelVersion> Versions { get; private set; }

    private IfcModel() 
    {
        FileName = string.Empty;
        OriginalFilePath = string.Empty;
        Metadata = ModelMetadata.Empty;
        ConversionJobs = new List<ConversionJob>();
        Versions = new List<ModelVersion>();
    }

    public static IfcModel Create(
        string fileName, 
        string originalFilePath, 
        long fileSizeBytes,
        Guid? userId = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));

        if (fileSizeBytes <= 0)
            throw new ArgumentException("File size must be greater than zero", nameof(fileSizeBytes));

        return new IfcModel
        {
            FileName = fileName,
            OriginalFilePath = originalFilePath,
            FileSizeBytes = fileSizeBytes,
            Status = ModelStatus.Uploaded,
            UserId = userId,
            Metadata = ModelMetadata.Empty
        };
    }

    public void UpdateMetadata(ModelMetadata metadata)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        MarkAsUpdated();
    }

    public void UpdateStatus(ModelStatus status)
    {
        Status = status;
        MarkAsUpdated();
    }

    public ConversionJob CreateConversionJob(ConversionFormat targetFormat)
    {
        var job = ConversionJob.Create(Id, targetFormat);
        ConversionJobs.Add(job);
        MarkAsUpdated();
        return job;
    }

    public ModelVersion CreateVersion(string versionNumber, string description)
    {
        var version = ModelVersion.Create(Id, versionNumber, description);
        Versions.Add(version);
        MarkAsUpdated();
        return version;
    }
}
