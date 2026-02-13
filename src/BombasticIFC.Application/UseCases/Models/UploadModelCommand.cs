using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Application.DTOs;
using BombasticIFC.Domain.Entities;
using BombasticIFC.Domain.Repositories;
using MediatR;

namespace BombasticIFC.Application.UseCases.Models;

/// <summary>
/// Command to upload a new IFC model
/// </summary>
public record UploadModelCommand(Stream FileStream, string FileName, Guid? UserId) 
    : IRequest<IfcModelDto>;

public class UploadModelCommandHandler : IRequestHandler<UploadModelCommand, IfcModelDto>
{
    private readonly IIfcModelRepository _modelRepository;
    private readonly IFileStorageService _fileStorageService;

    public UploadModelCommandHandler(
        IIfcModelRepository modelRepository,
        IFileStorageService fileStorageService)
    {
        _modelRepository = modelRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<IfcModelDto> Handle(UploadModelCommand request, CancellationToken cancellationToken)
    {
        // Save file to storage
        var filePath = await _fileStorageService.SaveFileAsync(
            request.FileStream, 
            request.FileName, 
            cancellationToken);

        // Get file size
        var fileSize = await _fileStorageService.GetFileSizeAsync(filePath, cancellationToken);

        // Create domain entity
        var model = IfcModel.Create(request.FileName, filePath, fileSize, request.UserId);

        // Save to repository
        await _modelRepository.AddAsync(model, cancellationToken);

        // Return DTO
        return new IfcModelDto
        {
            Id = model.Id,
            FileName = model.FileName,
            FileSizeBytes = model.FileSizeBytes,
            Status = model.Status,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }
}
