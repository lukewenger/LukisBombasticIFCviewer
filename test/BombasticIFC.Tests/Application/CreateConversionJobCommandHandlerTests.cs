using BombasticIFC.Application.UseCases.Conversion;
using BombasticIFC.Domain.Entities;
using BombasticIFC.Domain.Enums;
using BombasticIFC.Domain.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace BombasticIFC.Tests.Application;

public class CreateConversionJobCommandHandlerTests
{
    private readonly Mock<IIfcModelRepository> _modelRepoMock = new();
    private readonly Mock<IConversionJobRepository> _jobRepoMock = new();
    private readonly CreateConversionJobCommandHandler _handler;

    public CreateConversionJobCommandHandlerTests()
    {
        _handler = new CreateConversionJobCommandHandler(
            _modelRepoMock.Object,
            _jobRepoMock.Object);
    }

    private void SetupJobRepo()
    {
        _jobRepoMock
            .Setup(r => r.AddAsync(It.IsAny<ConversionJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversionJob j, CancellationToken _) => j);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsQueuedJobDto()
    {
        var model = IfcModel.Create("test.ifc", "/uploads/test.ifc", 1024);
        _modelRepoMock.Setup(r => r.GetByIdAsync(model.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);
        SetupJobRepo();

        var result = await _handler.Handle(
            new CreateConversionJobCommand(model.Id, ConversionFormat.XKT), default);

        result.Should().NotBeNull();
        result.ModelId.Should().Be(model.Id);
        result.TargetFormat.Should().Be(ConversionFormat.XKT);
        result.Status.Should().Be(ConversionStatus.Queued);
        result.ProgressPercentage.Should().Be(0);
        result.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_NonExistentModel_ThrowsInvalidOperationException()
    {
        var missingId = Guid.NewGuid();
        _modelRepoMock.Setup(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IfcModel?)null);

        var act = () => _handler.Handle(
            new CreateConversionJobCommand(missingId, ConversionFormat.XKT), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{missingId}*");
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsJobToRepository()
    {
        var model = IfcModel.Create("test.ifc", "/uploads/test.ifc", 1024);
        _modelRepoMock.Setup(r => r.GetByIdAsync(model.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);
        SetupJobRepo();

        await _handler.Handle(
            new CreateConversionJobCommand(model.Id, ConversionFormat.XKT), default);

        _jobRepoMock.Verify(r => r.AddAsync(
            It.Is<ConversionJob>(j =>
                j.ModelId == model.Id &&
                j.TargetFormat == ConversionFormat.XKT &&
                j.Status == ConversionStatus.Queued),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_AddsJobToModelCollection()
    {
        var model = IfcModel.Create("test.ifc", "/uploads/test.ifc", 1024);
        _modelRepoMock.Setup(r => r.GetByIdAsync(model.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);
        SetupJobRepo();

        await _handler.Handle(
            new CreateConversionJobCommand(model.Id, ConversionFormat.XKT), default);

        model.ConversionJobs.Should().HaveCount(1);
        model.ConversionJobs.First().TargetFormat.Should().Be(ConversionFormat.XKT);
        model.ConversionJobs.First().Status.Should().Be(ConversionStatus.Queued);
    }

    [Fact]
    public async Task Handle_WithXktFormat_ReturnedDtoHasCorrectFormat()
    {
        var model = IfcModel.Create("building.ifc", "/uploads/building.ifc", 2048);
        _modelRepoMock.Setup(r => r.GetByIdAsync(model.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);
        SetupJobRepo();

        var result = await _handler.Handle(
            new CreateConversionJobCommand(model.Id, ConversionFormat.XKT), default);

        result.TargetFormat.Should().Be(ConversionFormat.XKT);
    }
}
