using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Domain.Enums;
using BombasticIFC.Domain.Repositories;
using MediatR;

namespace BombasticIFC.Application.UseCases.Models;

public record GetModelOutputQuery(Guid ModelId) : IRequest<ModelFileResult?>;

public class GetModelOutputQueryHandler : IRequestHandler<GetModelOutputQuery, ModelFileResult?>
{
    private readonly IIfcModelRepository _modelRepository;
    private readonly IConversionJobRepository _conversionJobRepository;
    private readonly IFileStorageService _fileStorageService;

    public GetModelOutputQueryHandler(
        IIfcModelRepository modelRepository,
        IConversionJobRepository conversionJobRepository,
        IFileStorageService fileStorageService)
    {
        _modelRepository = modelRepository;
        _conversionJobRepository = conversionJobRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<ModelFileResult?> Handle(GetModelOutputQuery request, CancellationToken cancellationToken)
    {
        var model = await _modelRepository.GetByIdAsync(request.ModelId, cancellationToken);
        if (model == null) return null;

        var jobs = await _conversionJobRepository.GetByModelIdAsync(request.ModelId, cancellationToken);
        var completedJob = jobs
            .Where(j => j.Status == ConversionStatus.Completed && j.OutputFilePath != null)
            .OrderByDescending(j => j.CompletedAt)
            .FirstOrDefault();

        if (completedJob?.OutputFilePath == null) return null;
        if (!await _fileStorageService.FileExistsAsync(completedJob.OutputFilePath)) return null;

        var stream = await _fileStorageService.GetFileAsync(completedJob.OutputFilePath);
        return new ModelFileResult(stream, $"{request.ModelId}.xkt", "application/octet-stream");
    }
}
