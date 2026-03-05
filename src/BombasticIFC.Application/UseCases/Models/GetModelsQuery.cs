using BombasticIFC.Application.DTOs;
using BombasticIFC.Domain.Repositories;
using MediatR;

namespace BombasticIFC.Application.UseCases.Models;

/// <summary>
/// Query to get all IFC models
/// </summary>
public record GetModelsQuery() : IRequest<List<IfcModelDto>>;

public class GetModelsQueryHandler : IRequestHandler<GetModelsQuery, List<IfcModelDto>>
{
    private readonly IIfcModelRepository _modelRepository;

    public GetModelsQueryHandler(IIfcModelRepository modelRepository)
    {
        _modelRepository = modelRepository;
    }

    public async Task<List<IfcModelDto>> Handle(GetModelsQuery request, CancellationToken cancellationToken)
    {
        var models = await _modelRepository.GetAllAsync(cancellationToken);

        return models.Select(model => new IfcModelDto
        {
            Id = model.Id,
            FileName = model.FileName,
            FileSizeBytes = model.FileSizeBytes,
            Status = model.Status,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        }).ToList();
    }
}
