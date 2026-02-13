using BombasticIFC.Domain.Entities;
using BombasticIFC.Domain.Enums;
using BombasticIFC.Domain.Repositories;
using BombasticIFC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BombasticIFC.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for conversion jobs
/// </summary>
public class ConversionJobRepository : IConversionJobRepository
{
    private readonly ApplicationDbContext _context;

    public ConversionJobRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ConversionJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ConversionJobs
            .Include(j => j.Model)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<ConversionJob>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ConversionJobs
            .Include(j => j.Model)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ConversionJob>> GetByModelIdAsync(Guid modelId, CancellationToken cancellationToken = default)
    {
        return await _context.ConversionJobs
            .Where(j => j.ModelId == modelId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ConversionJob>> GetByStatusAsync(ConversionStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.ConversionJobs
            .Where(j => j.Status == status)
            .Include(j => j.Model)
            .ToListAsync(cancellationToken);
    }

    public async Task<ConversionJob> AddAsync(ConversionJob job, CancellationToken cancellationToken = default)
    {
        await _context.ConversionJobs.AddAsync(job, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task UpdateAsync(ConversionJob job, CancellationToken cancellationToken = default)
    {
        _context.ConversionJobs.Update(job);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await GetByIdAsync(id, cancellationToken);
        if (job != null)
        {
            _context.ConversionJobs.Remove(job);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
