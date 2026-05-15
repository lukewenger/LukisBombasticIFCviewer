using BombasticIFC.Application.DTOs;
using BombasticIFC.Application.UseCases.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace BombasticIFC.API.Controllers;

/// <summary>
/// Controller for IFC model operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ModelsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ModelsController> _logger;

    public ModelsController(
        IMediator mediator,
        ILogger<ModelsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all models
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<IfcModelDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<IfcModelDto>>> GetModels()
    {
        var query = new GetModelsQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Upload a new IFC model
    /// </summary>
    [HttpPost("upload")]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(524_288_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
    [ProducesResponseType(typeof(IfcModelDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IfcModelDto>> UploadModel(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        if (!file.FileName.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only IFC files are allowed");

        _logger.LogInformation("Uploading model: {FileName}, Size: {FileSize}", file.FileName, file.Length);

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub");
        Guid? userId = userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var parsedId)
            ? parsedId
            : null;

        using var stream = file.OpenReadStream();
        var command = new UploadModelCommand(stream, file.FileName, userId);
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

    /// <summary>
    /// Get the original IFC source file for a model
    /// </summary>
    [HttpGet("{id}/original")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModelOriginal(Guid id)
    {
        var result = await _mediator.Send(new GetModelOriginalQuery(id));
        if (result == null)
            return NotFound(new { message = "Model or original file not found" });
        return File(result.Stream, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Get the converted XKT output file for a model
    /// </summary>
    [HttpGet("{id}/output")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModelOutput(Guid id)
    {
        var result = await _mediator.Send(new GetModelOutputQuery(id));
        if (result == null)
            return NotFound(new { message = "No converted output available for this model" });
        return File(result.Stream, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Delete a model by ID
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteModel(Guid id)
    {
        try
        {
            await _mediator.Send(new DeleteModelCommand(id));
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }
}
