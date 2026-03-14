using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Domain.Entities;
using BombasticIFC.Domain.Enums;
using BombasticIFC.Domain.Repositories;
using BombasticIFC.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BombasticIFC.Tests.Conversion;

public class ConversionWorkerTests
{
    [Fact]
    public async Task Worker_ShouldRetryOnce_OnTransientFailure_AndCompleteJob()
    {
        var model = IfcModel.Create("sample.ifc", "/tmp/sample.ifc", 42);
        var job = model.CreateConversionJob(ConversionFormat.XKT);

        var modelRepoMock = new Mock<IIfcModelRepository>();
        modelRepoMock
            .Setup(r => r.GetByIdAsync(model.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);
        modelRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<IfcModel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var jobRepoMock = new Mock<IConversionJobRepository>();
        jobRepoMock
            .Setup(r => r.GetByStatusAsync(ConversionStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => job.Status == ConversionStatus.Queued
                ? new[] { job }
                : Array.Empty<ConversionJob>());
        jobRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<ConversionJob>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var conversionServiceMock = new Mock<IIfcConversionService>();
        conversionServiceMock
            .SetupSequence(s => s.ConvertAsync(
                model.OriginalFilePath,
                ConversionFormat.XKT,
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Transient timeout"))
            .ReturnsAsync("/tmp/sample.xkt");

        var scopeFactory = BuildScopeFactory(jobRepoMock.Object, modelRepoMock.Object, conversionServiceMock.Object);
        var worker = new TestableConversionWorker(scopeFactory, NullLogger<ConversionWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.RunUntilCancelled(cts.Token));

        conversionServiceMock.Verify(s => s.ConvertAsync(
            model.OriginalFilePath,
            ConversionFormat.XKT,
            It.IsAny<IProgress<int>?>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));

        job.Status.Should().Be(ConversionStatus.Completed);
        model.Status.Should().Be(ModelStatus.Ready);
        job.OutputFilePath.Should().Be("/tmp/sample.xkt");
    }

    [Fact]
    public async Task Worker_ShouldNotRetry_OnNonTransientFailure_AndFailJob()
    {
        var model = IfcModel.Create("invalid.ifc", "/tmp/invalid.ifc", 42);
        var job = model.CreateConversionJob(ConversionFormat.XKT);

        var modelRepoMock = new Mock<IIfcModelRepository>();
        modelRepoMock
            .Setup(r => r.GetByIdAsync(model.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);
        modelRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<IfcModel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var jobRepoMock = new Mock<IConversionJobRepository>();
        jobRepoMock
            .Setup(r => r.GetByStatusAsync(ConversionStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => job.Status == ConversionStatus.Queued
                ? new[] { job }
                : Array.Empty<ConversionJob>());
        jobRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<ConversionJob>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var conversionServiceMock = new Mock<IIfcConversionService>();
        conversionServiceMock
            .Setup(s => s.ConvertAsync(
                model.OriginalFilePath,
                ConversionFormat.XKT,
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidDataException("Invalid IFC header"));

        var scopeFactory = BuildScopeFactory(jobRepoMock.Object, modelRepoMock.Object, conversionServiceMock.Object);
        var worker = new TestableConversionWorker(scopeFactory, NullLogger<ConversionWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.RunUntilCancelled(cts.Token));

        conversionServiceMock.Verify(s => s.ConvertAsync(
            model.OriginalFilePath,
            ConversionFormat.XKT,
            It.IsAny<IProgress<int>?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        job.Status.Should().Be(ConversionStatus.Failed);
        model.Status.Should().Be(ModelStatus.Failed);
        job.ErrorMessage.Should().Contain("Invalid IFC header");
    }

    private static IServiceScopeFactory BuildScopeFactory(
        IConversionJobRepository jobRepository,
        IIfcModelRepository modelRepository,
        IIfcConversionService conversionService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(jobRepository);
        services.AddSingleton(modelRepository);
        services.AddSingleton(conversionService);

        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private sealed class TestableConversionWorker : ConversionWorker
    {
        public TestableConversionWorker(
            IServiceScopeFactory scopeFactory,
            Microsoft.Extensions.Logging.ILogger<ConversionWorker> logger)
            : base(scopeFactory, logger)
        {
        }

        public Task RunUntilCancelled(CancellationToken token) => base.ExecuteAsync(token);
    }
}
