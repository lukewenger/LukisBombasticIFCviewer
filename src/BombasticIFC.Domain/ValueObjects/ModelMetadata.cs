namespace BombasticIFC.Domain.ValueObjects;

/// <summary>
/// Value object representing metadata extracted from IFC models
/// </summary>
public class ModelMetadata
{
    public string IfcSchema { get; private set; }
    public string ProjectName { get; private set; }
    public string ApplicationName { get; private set; }
    public int NumberOfElements { get; private set; }
    public Dictionary<string, int> ElementTypeCounts { get; private set; }

    private ModelMetadata()
    {
        IfcSchema = string.Empty;
        ProjectName = string.Empty;
        ApplicationName = string.Empty;
        ElementTypeCounts = new Dictionary<string, int>();
    }

    public static ModelMetadata Empty => new();

    public static ModelMetadata Create(
        string ifcSchema,
        string projectName,
        string applicationName,
        int numberOfElements,
        Dictionary<string, int>? elementTypeCounts = null)
    {
        return new ModelMetadata
        {
            IfcSchema = ifcSchema ?? string.Empty,
            ProjectName = projectName ?? string.Empty,
            ApplicationName = applicationName ?? string.Empty,
            NumberOfElements = numberOfElements,
            ElementTypeCounts = elementTypeCounts ?? new Dictionary<string, int>()
        };
    }

    public override bool Equals(object? obj)
    {
        if (obj is not ModelMetadata other)
            return false;

        return IfcSchema == other.IfcSchema &&
               ProjectName == other.ProjectName &&
               ApplicationName == other.ApplicationName &&
               NumberOfElements == other.NumberOfElements;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(IfcSchema, ProjectName, ApplicationName, NumberOfElements);
    }
}
