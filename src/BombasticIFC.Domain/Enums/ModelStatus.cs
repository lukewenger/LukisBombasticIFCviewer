namespace BombasticIFC.Domain.Enums;

/// <summary>
/// Status of an IFC model in the system
/// </summary>
public enum ModelStatus
{
    Uploaded = 0,
    Processing = 1,
    Ready = 2,
    Failed = 3,
    Archived = 4
}
