using BombasticIFC.Application.DTOs;
using BombasticIFC.Application.UseCases.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BombasticIFC.API.Controllers;

/// <summary>
/// Controller for IFC model operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ModelsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ModelsController> _logger;

    public ModelsController(IMediator mediator, ILogger<ModelsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Upload a new IFC model
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(IfcModelDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IfcModelDto>> UploadModel(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        if (!file.FileName.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only IFC files are allowed");

        _logger.LogInformation("Uploading model: {FileName}, Size: {FileSize}", file.FileName, file.Length);

        using var stream = file.OpenReadStream();
        var command = new UploadModelCommand(stream, file.FileName, null);
        var result = await _mediator.Send(command);

        return CreatedAtAction(nameof(GetModel), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get a model by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(IfcModelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IfcModelDto>> GetModel(Guid id)
    {
        var query = new GetModelByIdQuery(id);
        var result = await _mediator.Send(query);

        if (result == null)
            return NotFound();

        return Ok(result);
    }
}
