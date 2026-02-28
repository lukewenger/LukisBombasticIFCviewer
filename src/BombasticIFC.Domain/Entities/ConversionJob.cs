using BombasticIFC.Domain.Enums;

namespace BombasticIFC.Domain.Entities;

/// <summary>
/// Represents a conversion job for an IFC model
/// </summary>
public class ConversionJob : BaseEntity
{
    public Guid ModelId { get; private set; }
    public ConversionFormat TargetFormat { get; private set; }
    public ConversionStatus Status { get; private set; }
    public int ProgressPercentage { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? OutputFilePath { get; private set; }
    public string? ErrorMessage { get; private set; }

    // Navigation
    public IfcModel Model { get; private set; } = null!;

    private ConversionJob() { }

    public static ConversionJob Create(Guid modelId, ConversionFormat targetFormat)
    {
        if (modelId == Guid.Empty)
            throw new ArgumentException("Model ID cannot be empty", nameof(modelId));

        return new ConversionJob
        {
            ModelId = modelId,
            TargetFormat = targetFormat,
            Status = ConversionStatus.Queued,
            ProgressPercentage = 0
        };
    }

    public void StartProcessing()
    {
        Status = ConversionStatus.Processing;
        StartedAt = DateTime.UtcNow;
        MarkAsUpdated();
    }

    public void UpdateProgress(int percentage)
    {
        ProgressPercentage = Math.Clamp(percentage, 0, 100);
        MarkAsUpdated();
    }

    public void Complete(string outputFilePath)
    {
        if (string.IsNullOrWhiteSpace(outputFilePath))
            throw new ArgumentException("Output file path cannot be empty", nameof(outputFilePath));

        Status = ConversionStatus.Completed;
        ProgressPercentage = 100;
        OutputFilePath = outputFilePath;
        CompletedAt = DateTime.UtcNow;
        MarkAsUpdated();
    }

    public void Fail(string errorMessage)
    {
        Status = ConversionStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
        MarkAsUpdated();
    }

    public void Cancel()
    {
        Status = ConversionStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        MarkAsUpdated();
    }
}
