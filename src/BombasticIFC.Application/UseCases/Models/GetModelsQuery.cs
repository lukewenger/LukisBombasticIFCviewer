using BombasticIFC.Application.DTOs;
using BombasticIFC.Domain.Repositories;
using BombasticIFC.Domain.Enums;
using MediatR;

namespace BombasticIFC.Application.UseCases.Models;

/// <summary>
/// Query to get all IFC models
/// </summary>
public record GetModelsQuery() : IRequest<List<IfcModelDto>>;

public class GetModelsQueryHandler : IRequestHandler<GetModelsQuery, List<IfcModelDto>>
{
    private readonly IIfcModelRepository _modelRepository;
    private readonly IConversionJobRepository _jobRepository;

    public GetModelsQueryHandler(
        IIfcModelRepository modelRepository,
        IConversionJobRepository jobRepository)
    {
        _modelRepository = modelRepository;
        _jobRepository = jobRepository;
    }

    public async Task<List<IfcModelDto>> Handle(GetModelsQuery request, CancellationToken cancellationToken)
    {
        var models = await _modelRepository.GetAllAsync(cancellationToken);

        // Fetch all completed jobs in one query and group by model ID to avoid N+1
        var completedJobs = await _jobRepository.GetByStatusAsync(ConversionStatus.Completed, cancellationToken);
        var latestCompletedByModel = completedJobs
            .Where(j => j.OutputFilePath != null)
            .GroupBy(j => j.ModelId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(j => j.CompletedAt).First());

        return models.Select(model =>
        {
            var hasXkt = latestCompletedByModel.ContainsKey(model.Id);
            return new IfcModelDto
            {
                Id = model.Id,
                FileName = model.FileName,
                FileSizeBytes = model.FileSizeBytes,
                Status = model.Status,
                CreatedAt = model.CreatedAt,
                UpdatedAt = model.UpdatedAt,
                XktOutputUrl = hasXkt ? $"/api/models/{model.Id}/output" : null,
                OriginalFileUrl = $"/api/models/{model.Id}/original"
            };
        }).ToList();
    }
}
