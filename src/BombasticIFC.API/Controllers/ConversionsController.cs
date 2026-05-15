using BombasticIFC.Application.DTOs;
using BombasticIFC.Application.UseCases.Conversion;
using BombasticIFC.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BombasticIFC.API.Controllers;

/// <summary>
/// Controller for conversion operations
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ConversionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ConversionsController> _logger;

    public ConversionsController(IMediator mediator, ILogger<ConversionsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Create a new conversion job
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("api")]
    [ProducesResponseType(typeof(ConversionJobDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConversionJobDto>> CreateConversionJob(
        [FromBody] CreateConversionRequest request)
    {
        _logger.LogInformation(
            "Creating conversion job for model {ModelId} to format {Format}", 
            request.ModelId, 
            request.TargetFormat);

        var command = new CreateConversionJobCommand(request.ModelId, request.TargetFormat);
        var result = await _mediator.Send(command);

        return CreatedAtAction(nameof(GetConversionJob), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get conversion job status
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ConversionJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversionJobDto>> GetConversionJob(Guid id)
    {
        var query = new GetConversionJobQuery(id);
        var result = await _mediator.Send(query);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// Retry a failed conversion job
    /// </summary>
    [HttpPost("{id}/retry")]
    [ProducesResponseType(typeof(ConversionJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversionJobDto>> RetryConversionJob(Guid id)
    {
        try
        {
            var result = await _mediator.Send(new RetryConversionJobCommand(id));
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { message = ex.Message })
                : BadRequest(new { message = ex.Message });
        }
    }
}

public record CreateConversionRequest(Guid ModelId, ConversionFormat TargetFormat);
