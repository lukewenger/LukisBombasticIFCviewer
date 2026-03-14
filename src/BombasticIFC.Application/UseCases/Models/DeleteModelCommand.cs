using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Domain.Repositories;
using MediatR;

namespace BombasticIFC.Application.UseCases.Models;

/// <summary>
/// Command to delete an IFC model and related files/jobs.
/// </summary>
public record DeleteModelCommand(Guid ModelId) : IRequest<Unit>;

public class DeleteModelCommandHandler : IRequestHandler<DeleteModelCommand, Unit>
{
    private readonly IIfcModelRepository _modelRepository;
    private readonly IConversionJobRepository _conversionJobRepository;
    private readonly IFileStorageService _fileStorageService;

    public DeleteModelCommandHandler(
        IIfcModelRepository modelRepository,
        IConversionJobRepository conversionJobRepository,
        IFileStorageService fileStorageService)
    {
        _modelRepository = modelRepository;
        _conversionJobRepository = conversionJobRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<Unit> Handle(DeleteModelCommand request, CancellationToken cancellationToken)
    {
        var model = await _modelRepository.GetByIdAsync(request.ModelId, cancellationToken);
        if (model == null)
            throw new InvalidOperationException($"Model with ID {request.ModelId} not found");

        if (!string.IsNullOrWhiteSpace(model.OriginalFilePath) &&
            await _fileStorageService.FileExistsAsync(model.OriginalFilePath, cancellationToken))
        {
            await _fileStorageService.DeleteFileAsync(model.OriginalFilePath, cancellationToken);
        }

        var conversionJobs = await _conversionJobRepository.GetByModelIdAsync(request.ModelId, cancellationToken);
        foreach (var job in conversionJobs)
        {
            if (!string.IsNullOrWhiteSpace(job.OutputFilePath) &&
                await _fileStorageService.FileExistsAsync(job.OutputFilePath, cancellationToken))
            {
                await _fileStorageService.DeleteFileAsync(job.OutputFilePath, cancellationToken);
            }

            await _conversionJobRepository.DeleteAsync(job.Id, cancellationToken);
        }

        await _modelRepository.DeleteAsync(request.ModelId, cancellationToken);

        return Unit.Value;
    }
}