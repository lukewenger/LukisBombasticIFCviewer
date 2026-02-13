using BombasticIFC.Application.DTOs;
using BombasticIFC.Domain.Repositories;
using MediatR;

namespace BombasticIFC.Application.UseCases.Models;

/// <summary>
/// Query to get a model by ID
/// </summary>
public record GetModelByIdQuery(Guid ModelId) : IRequest<IfcModelDto?>;

public class GetModelByIdQueryHandler : IRequestHandler<GetModelByIdQuery, IfcModelDto?>
{
    private readonly IIfcModelRepository _modelRepository;

    public GetModelByIdQueryHandler(IIfcModelRepository modelRepository)
    {
        _modelRepository = modelRepository;
    }

    public async Task<IfcModelDto?> Handle(GetModelByIdQuery request, CancellationToken cancellationToken)
    {
        var model = await _modelRepository.GetByIdAsync(request.ModelId, cancellationToken);

        if (model == null)
            return null;

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
