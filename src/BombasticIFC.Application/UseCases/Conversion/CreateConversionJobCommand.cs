using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Application.DTOs;
using BombasticIFC.Domain.Enums;
using BombasticIFC.Domain.Repositories;
using MediatR;

namespace BombasticIFC.Application.UseCases.Conversion;

/// <summary>
/// Command to create a conversion job
/// </summary>
public record CreateConversionJobCommand(Guid ModelId, ConversionFormat TargetFormat) 
    : IRequest<ConversionJobDto>;

public class CreateConversionJobCommandHandler 
    : IRequestHandler<CreateConversionJobCommand, ConversionJobDto>
{
    private readonly IIfcModelRepository _modelRepository;
    private readonly IConversionJobRepository _jobRepository;

    public CreateConversionJobCommandHandler(
        IIfcModelRepository modelRepository,
        IConversionJobRepository jobRepository)
    {
        _modelRepository = modelRepository;
        _jobRepository = jobRepository;
    }

    public async Task<ConversionJobDto> Handle(
        CreateConversionJobCommand request, 
        CancellationToken cancellationToken)
    {
        // Get the model
        var model = await _modelRepository.GetByIdAsync(request.ModelId, cancellationToken)
            ?? throw new InvalidOperationException($"Model with ID {request.ModelId} not found");

        // Create conversion job
        var job = model.CreateConversionJob(request.TargetFormat);

        // Save
        await _jobRepository.AddAsync(job, cancellationToken);

        return new ConversionJobDto
        {
            Id = job.Id,
            ModelId = job.ModelId,
            TargetFormat = job.TargetFormat,
            Status = job.Status,
            ProgressPercentage = job.ProgressPercentage,
            CreatedAt = job.CreatedAt
        };
    }
}
