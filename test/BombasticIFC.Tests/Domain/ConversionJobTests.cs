using BombasticIFC.Domain.Entities;
using BombasticIFC.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BombasticIFC.Tests.Domain;

public class ConversionJobTests
{
    [Fact]
    public void Create_WithValidModelId_SetsQueuedStatus()
    {
        var modelId = Guid.NewGuid();

        var job = ConversionJob.Create(modelId, ConversionFormat.XKT);

        job.Status.Should().Be(ConversionStatus.Queued);
        job.TargetFormat.Should().Be(ConversionFormat.XKT);
        job.ProgressPercentage.Should().Be(0);
        job.ModelId.Should().Be(modelId);
        job.OutputFilePath.Should().BeNull();
        job.ErrorMessage.Should().BeNull();
        job.StartedAt.Should().BeNull();
        job.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyModelId_ThrowsArgumentException()
    {
        var act = () => ConversionJob.Create(Guid.Empty, ConversionFormat.XKT);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("modelId");
    }

    [Fact]
    public void Create_AssignsNewId()
    {
        var job1 = ConversionJob.Create(Guid.NewGuid(), ConversionFormat.XKT);
        var job2 = ConversionJob.Create(Guid.NewGuid(), ConversionFormat.XKT);

        job1.Id.Should().NotBe(Guid.Empty);
        job2.Id.Should().NotBe(Guid.Empty);
        job1.Id.Should().NotBe(job2.Id);
    }

    [Fact]
    public void StartProcessing_ChangesStatusToProcessing()
    {
        var job = ConversionJob.Create(Guid.NewGuid(), ConversionFormat.XKT);

        job.StartProcessing();

        job.Status.Should().Be(ConversionStatus.Processing);
        job.StartedAt.Should().NotBeNull();
        job.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(150, 100)]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    public void UpdateProgress_ClampsValueBetween0And100(int input, int expected)
    {
        var job = ConversionJob.Create(Guid.NewGuid(), ConversionFormat.XKT);

        job.UpdateProgress(input);

        job.ProgressPercentage.Should().Be(expected);
    }

    [Fact]
    public void Complete_WithValidPath_SetsCompletedStatus()
    {
        var job = ConversionJob.Create(Guid.NewGuid(), ConversionFormat.XKT);
        job.StartProcessing();

        job.Complete("/output/model.xkt");

        job.Status.Should().Be(ConversionStatus.Completed);
        job.ProgressPercentage.Should().Be(100);
        job.OutputFilePath.Should().Be("/output/model.xkt");
        job.CompletedAt.Should().NotBeNull();
        job.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Complete_WithEmptyOrWhitespacePath_ThrowsArgumentException(string path)
    {
        var job = ConversionJob.Create(Guid.NewGuid(), ConversionFormat.XKT);

        var act = () => job.Complete(path);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("outputFilePath");
    }

    [Fact]
    public void Fail_SetsFailedStatusAndErrorMessage()
    {
        var job = ConversionJob.Create(Guid.NewGuid(), ConversionFormat.XKT);
        job.StartProcessing();

        job.Fail("ifc-convert exited with code 1");

        job.Status.Should().Be(ConversionStatus.Failed);
        job.ErrorMessage.Should().Be("ifc-convert exited with code 1");
        job.CompletedAt.Should().NotBeNull();
        job.OutputFilePath.Should().BeNull();
    }

    [Fact]
    public void Cancel_SetsCancelledStatus()
    {
        var job = ConversionJob.Create(Guid.NewGuid(), ConversionFormat.XKT);

        job.Cancel();

        job.Status.Should().Be(ConversionStatus.Cancelled);
        job.CompletedAt.Should().NotBeNull();
    }
}
