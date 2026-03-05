using BombasticIFC.Application.DTOs;
using BombasticIFC.Domain.Repositories;
using MediatR;

namespace BombasticIFC.Application.UseCases.Conversion;

/// <summary>
/// Query to get a conversion job by ID
/// </summary>
public record GetConversionJobQuery(Guid JobId) : IRequest<ConversionJobDto?>;

public class GetConversionJobQueryHandler : IRequestHandler<GetConversionJobQuery, ConversionJobDto?>
{
    private readonly IConversionJobRepository _jobRepository;

    public GetConversionJobQueryHandler(IConversionJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<ConversionJobDto?> Handle(GetConversionJobQuery request, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken);

        if (job == null)
            return null;

        return new ConversionJobDto
        {
            Id = job.Id,
            ModelId = job.ModelId,
            TargetFormat = job.TargetFormat,
            Status = job.Status,
            ProgressPercentage = job.ProgressPercentage,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            OutputFilePath = job.OutputFilePath,
            ErrorMessage = job.ErrorMessage
        };
    }
}
