using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Domain.Enums;
using BombasticIFC.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BombasticIFC.Infrastructure.Services;

/// <summary>
/// Background service that polls for queued conversion jobs and processes them sequentially.
/// </summary>
public class ConversionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConversionWorker> _logger;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    public ConversionWorker(IServiceScopeFactory scopeFactory, ILogger<ConversionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ConversionWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueuedJobsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in ConversionWorker loop.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("ConversionWorker stopped.");
    }

    private async Task ProcessQueuedJobsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IConversionJobRepository>();
        var modelRepository = scope.ServiceProvider.GetRequiredService<IIfcModelRepository>();
        var conversionService = scope.ServiceProvider.GetRequiredService<IIfcConversionService>();

        var queuedJobs = await jobRepository.GetByStatusAsync(ConversionStatus.Queued, stoppingToken);

        foreach (var job in queuedJobs)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            var model = await modelRepository.GetByIdAsync(job.ModelId, stoppingToken);
            if (model is null)
            {
                _logger.LogWarning("Model {ModelId} not found for job {JobId}. Skipping.", job.ModelId, job.Id);
                continue;
            }

            _logger.LogInformation("Processing conversion job {JobId} for model {ModelId}.", job.Id, model.Id);

            job.StartProcessing();
            model.UpdateStatus(ModelStatus.Processing);
            await jobRepository.UpdateAsync(job, stoppingToken);
            await modelRepository.UpdateAsync(model, stoppingToken);

            try
            {
                var progress = new Progress<int>(pct =>
                {
                    job.UpdateProgress(pct);
                    // Fire-and-forget progress persist (best-effort, not awaited inside callback)
                    _ = jobRepository.UpdateAsync(job, CancellationToken.None);
                });

                var xktPath = await conversionService.ConvertAsync(
                    model.OriginalFilePath,
                    ConversionFormat.XKT,
                    progress,
                    stoppingToken);

                job.Complete(xktPath);
                model.UpdateStatus(ModelStatus.Ready);
                await jobRepository.UpdateAsync(job, stoppingToken);
                await modelRepository.UpdateAsync(model, stoppingToken);

                _logger.LogInformation("Job {JobId} completed. Output: {XktPath}", job.Id, xktPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId} failed.", job.Id);

                job.Fail(ex.Message);
                model.UpdateStatus(ModelStatus.Failed);
                await jobRepository.UpdateAsync(job, stoppingToken);
                await modelRepository.UpdateAsync(model, stoppingToken);
            }
        }
    }
}
