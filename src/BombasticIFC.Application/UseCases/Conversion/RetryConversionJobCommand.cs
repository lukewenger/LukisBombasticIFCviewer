using BombasticIFC.Application.DTOs;
using BombasticIFC.Domain.Enums;
using BombasticIFC.Domain.Repositories;
using MediatR;

namespace BombasticIFC.Application.UseCases.Conversion;

public record RetryConversionJobCommand(Guid JobId) : IRequest<ConversionJobDto>;

public class RetryConversionJobCommandHandler : IRequestHandler<RetryConversionJobCommand, ConversionJobDto>
{
    private readonly IConversionJobRepository _jobRepository;
    private readonly IIfcModelRepository _modelRepository;

    public RetryConversionJobCommandHandler(
        IConversionJobRepository jobRepository,
        IIfcModelRepository modelRepository)
    {
        _jobRepository = jobRepository;
        _modelRepository = modelRepository;
    }

    public async Task<ConversionJobDto> Handle(RetryConversionJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversion job {request.JobId} not found");

        if (job.Status != ConversionStatus.Failed)
            throw new InvalidOperationException($"Only failed jobs can be retried. Current status: {job.Status}");

        job.ResetToQueued();
        await _jobRepository.UpdateAsync(job, cancellationToken);

        var model = await _modelRepository.GetByIdAsync(job.ModelId, cancellationToken);
        if (model != null)
        {
            model.UpdateStatus(Domain.Enums.ModelStatus.Uploaded);
            await _modelRepository.UpdateAsync(model, cancellationToken);
        }

        return new ConversionJobDto
        {
            Id = job.Id,
            ModelId = job.ModelId,
            TargetFormat = job.TargetFormat,
            Status = job.Status,
            ProgressPercentage = job.ProgressPercentage,
            CreatedAt = job.CreatedAt,
            HasOutput = false
        };
    }
}
