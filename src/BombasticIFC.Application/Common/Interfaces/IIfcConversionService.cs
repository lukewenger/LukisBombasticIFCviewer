using BombasticIFC.Domain.Enums;

namespace BombasticIFC.Application.Common.Interfaces;

/// <summary>
/// Service for IFC model conversion
/// </summary>
public interface IIfcConversionService
{
    Task<string> ConvertAsync(
        string sourceFilePath, 
        ConversionFormat targetFormat, 
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
    
    Task<bool> IsConversionSupportedAsync(ConversionFormat format);
}
