using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Domain.Repositories;
using MediatR;

namespace BombasticIFC.Application.UseCases.Models;

public record GetModelOriginalQuery(Guid ModelId) : IRequest<ModelFileResult?>;

public record ModelFileResult(Stream Stream, string FileName, string ContentType);

public class GetModelOriginalQueryHandler : IRequestHandler<GetModelOriginalQuery, ModelFileResult?>
{
    private readonly IIfcModelRepository _modelRepository;
    private readonly IFileStorageService _fileStorageService;

    public GetModelOriginalQueryHandler(IIfcModelRepository modelRepository, IFileStorageService fileStorageService)
    {
        _modelRepository = modelRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<ModelFileResult?> Handle(GetModelOriginalQuery request, CancellationToken cancellationToken)
    {
        var model = await _modelRepository.GetByIdAsync(request.ModelId, cancellationToken);
        if (model == null) return null;
        if (!await _fileStorageService.FileExistsAsync(model.OriginalFilePath)) return null;

        var stream = await _fileStorageService.GetFileAsync(model.OriginalFilePath);
        var fileName = Path.GetFileName(model.OriginalFilePath);
        return new ModelFileResult(stream, fileName, "application/octet-stream");
    }
}
