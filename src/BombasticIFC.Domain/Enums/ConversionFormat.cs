namespace BombasticIFC.Domain.Enums;

/// <summary>
/// Target formats for IFC model conversion
/// </summary>
public enum ConversionFormat
{
    XKT = 0,      // xeokit format
    GLTF = 1,     // glTF format
    GLB = 2,      // Binary glTF
    JSON = 3      // Custom JSON format
}
