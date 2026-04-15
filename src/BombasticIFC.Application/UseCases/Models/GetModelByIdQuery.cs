using BombasticIFC.Application.DTOs;
using BombasticIFC.Domain.Repositories;
using BombasticIFC.Domain.Enums;
using MediatR;

namespace BombasticIFC.Application.UseCases.Models;

/// <summary>
/// Query to get a model by ID
/// </summary>
public record GetModelByIdQuery(Guid ModelId) : IRequest<IfcModelDto?>;

public class GetModelByIdQueryHandler : IRequestHandler<GetModelByIdQuery, IfcModelDto?>
{
    private readonly IIfcModelRepository _modelRepository;
    private readonly IConversionJobRepository _jobRepository;

    public GetModelByIdQueryHandler(
        IIfcModelRepository modelRepository,
        IConversionJobRepository jobRepository)
    {
        _modelRepository = modelRepository;
        _jobRepository = jobRepository;
    }

    public async Task<IfcModelDto?> Handle(GetModelByIdQuery request, CancellationToken cancellationToken)
    {
        var model = await _modelRepository.GetByIdAsync(request.ModelId, cancellationToken);

        if (model == null)
            return null;

        var jobs = await _jobRepository.GetByModelIdAsync(model.Id, cancellationToken);
        var latestCompleted = jobs
            .Where(j => j.Status == ConversionStatus.Completed && j.OutputFilePath != null)
            .OrderByDescending(j => j.CompletedAt)
            .FirstOrDefault();
        var latestFailed = jobs
            .Where(j => j.Status == ConversionStatus.Failed)
            .OrderByDescending(j => j.CompletedAt)
            .FirstOrDefault();

        return new IfcModelDto
        {
            Id = model.Id,
            FileName = model.FileName,
            FileSizeBytes = model.FileSizeBytes,
            Status = model.Status,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            XktOutputUrl = latestCompleted != null ? $"/api/models/{model.Id}/output" : null,
            OriginalFileUrl = $"/api/models/{model.Id}/original",
            ConversionError = latestFailed?.ErrorMessage
        };
    }
}
