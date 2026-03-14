using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Application.DTOs;
using BombasticIFC.Application.UseCases.Models;
using BombasticIFC.Domain.Repositories;
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
    private readonly IConversionJobRepository _conversionJobRepository;
    private readonly IFileStorageService _fileStorageService;

    private readonly IIfcModelRepository _modelRepository;

    public ModelsController(
        IMediator mediator,
        ILogger<ModelsController> logger,
        IConversionJobRepository conversionJobRepository,
        IFileStorageService fileStorageService,
        IIfcModelRepository modelRepository)
    {
        _mediator = mediator;
        _logger = logger;
        _conversionJobRepository = conversionJobRepository;
        _fileStorageService = fileStorageService;
        _modelRepository = modelRepository;
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

    /// <summary>
    /// Get the original IFC source file for a model
    /// </summary>
    [HttpGet("{id}/original")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModelOriginal(Guid id)
    {
        var model = await _modelRepository.GetByIdAsync(id);
        if (model == null)
            return NotFound(new { message = "Model not found" });

        if (!await _fileStorageService.FileExistsAsync(model.OriginalFilePath))
            return NotFound(new { message = "Original file not found on disk" });

        var stream = await _fileStorageService.GetFileAsync(model.OriginalFilePath);
        var fileName = Path.GetFileName(model.OriginalFilePath);
        return File(stream, "application/octet-stream", fileName);
    }

    /// <summary>
    /// Get the converted XKT output file for a model
    /// </summary>
    [HttpGet("{id}/output")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModelOutput(Guid id)
    {
        var jobs = await _conversionJobRepository.GetByModelIdAsync(id);
        var completedJob = jobs
            .Where(j => j.Status == Domain.Enums.ConversionStatus.Completed && j.OutputFilePath != null)
            .OrderByDescending(j => j.CompletedAt)
            .FirstOrDefault();

        if (completedJob?.OutputFilePath == null)
        {
            return NotFound(new { message = "No converted output available for this model" });
        }

        if (!await _fileStorageService.FileExistsAsync(completedJob.OutputFilePath))
        {
            Response.ContentType = "application/json";
            return NotFound(new { message = "Output file not found on disk" });
        }

        var stream = await _fileStorageService.GetFileAsync(completedJob.OutputFilePath);
        var fileName = $"{id}.xkt";
        return File(stream, "application/octet-stream", fileName);
    }
}
