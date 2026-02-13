namespace BombasticIFC.Domain.Enums;

/// <summary>
/// Status of a conversion job
/// </summary>
public enum ConversionStatus
{
    Queued = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
