using BombasticIFC.Domain.Entities;
using BombasticIFC.Domain.Enums;

namespace BombasticIFC.Domain.Repositories;

/// <summary>
/// Repository interface for conversion jobs
/// </summary>
public interface IConversionJobRepository
{
    Task<ConversionJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ConversionJob>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ConversionJob>> GetByModelIdAsync(Guid modelId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ConversionJob>> GetByStatusAsync(ConversionStatus status, CancellationToken cancellationToken = default);
    Task<ConversionJob> AddAsync(ConversionJob job, CancellationToken cancellationToken = default);
    Task UpdateAsync(ConversionJob job, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
