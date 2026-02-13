using BombasticIFC.Domain.Entities;
using BombasticIFC.Domain.Repositories;
using BombasticIFC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BombasticIFC.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for IFC models
/// </summary>
public class IfcModelRepository : IIfcModelRepository
{
    private readonly ApplicationDbContext _context;

    public IfcModelRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IfcModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.IfcModels
            .Include(m => m.ConversionJobs)
            .Include(m => m.Versions)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<IfcModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.IfcModels
            .Include(m => m.ConversionJobs)
            .Include(m => m.Versions)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<IfcModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.IfcModels
            .Where(m => m.UserId == userId)
            .Include(m => m.ConversionJobs)
            .Include(m => m.Versions)
            .ToListAsync(cancellationToken);
    }

    public async Task<IfcModel> AddAsync(IfcModel model, CancellationToken cancellationToken = default)
    {
        await _context.IfcModels.AddAsync(model, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return model;
    }

    public async Task UpdateAsync(IfcModel model, CancellationToken cancellationToken = default)
    {
        _context.IfcModels.Update(model);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var model = await GetByIdAsync(id, cancellationToken);
        if (model != null)
        {
            model.MarkAsDeleted();
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.IfcModels.AnyAsync(m => m.Id == id, cancellationToken);
    }
}
