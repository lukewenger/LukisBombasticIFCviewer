using BombasticIFC.Domain.Entities;

namespace BombasticIFC.Application.Common.Interfaces;

/// <summary>
/// Database context interface for the application layer
/// </summary>
public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
