using BombasticIFC.Domain.Entities;

namespace BombasticIFC.Domain.Repositories;

/// <summary>
/// Repository interface for IFC models
/// </summary>
public interface IIfcModelRepository
{
    Task<IfcModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<IfcModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<IfcModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IfcModel> AddAsync(IfcModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(IfcModel model, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
