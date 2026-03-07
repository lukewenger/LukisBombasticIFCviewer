using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Application.UseCases.Conversion;
using BombasticIFC.Domain.Entities;
using BombasticIFC.Domain.Enums;
using BombasticIFC.Domain.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace BombasticIFC.Tests.Conversion;

/// <summary>
/// Tests for the IFC-to-XKT conversion pipeline using the three sample IFC files
/// located in test/Sample/. Since no production converter is wired up yet,
/// IIfcConversionService is mocked; these tests verify that:
///   - the sample files are present and valid IFC content,
///   - the command handler passes the correct file paths through,
///   - ConversionJob state transitions work end-to-end for each file.
/// </summary>
public class IfcToXktConversionTests
{
    /// <summary>Invokes the callback synchronously on Report — avoids async SynchronizationContext issues in tests.</summary>
    private sealed class SyncProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }

    private static readonly string SamplesDirectory =
        Path.Combine(AppContext.BaseDirectory, "Samples");

    public static IEnumerable<object[]> SampleIfcFiles =>
    [
        ["ELEMENT_C_1.OG_C1_STFT.ifc"],
        ["KRAG_A_OG2_A3_DB.ifc"],
        ["MW_A_OG2_A6_BN.ifc"],
    ];

    // ── File presence & validity ─────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(SampleIfcFiles))]
    public void SampleFile_Exists_AndIsNonEmpty(string fileName)
    {
        var path = Path.Combine(SamplesDirectory, fileName);

        File.Exists(path).Should().BeTrue(
            because: $"sample file '{fileName}' must be present in the test output");

        new FileInfo(path).Length.Should().BeGreaterThan(0,
            because: "a valid IFC file must not be empty");
    }

    [Theory]
    [MemberData(nameof(SampleIfcFiles))]
    public void SampleFile_HasIfcExtension(string fileName)
    {
        Path.GetExtension(fileName).Should().Be(".ifc",
            because: "IFC files must carry the .ifc extension");
    }

    [Theory]
    [MemberData(nameof(SampleIfcFiles))]
    public void SampleFile_StartsWithStepHeader(string fileName)
    {
        var path = Path.Combine(SamplesDirectory, fileName);
        var firstLine = File.ReadLines(path).First();

        firstLine.Trim().Should().StartWith("ISO-10303-21",
            because: "valid IFC files begin with the STEP Physical File (ISO-10303-21) header");
    }

    [Theory]
    [MemberData(nameof(SampleIfcFiles))]
    public void SampleFile_ContainsIfcSchema(string fileName)
    {
        var path = Path.Combine(SamplesDirectory, fileName);
        var header = File.ReadAllText(path)[..Math.Min(4096, (int)new FileInfo(path).Length)];

        header.Should().Contain("IFC",
            because: "the STEP header must declare an IFC schema (e.g. IFC2X3 or IFC4)");
    }

    // ── IIfcConversionService contract ───────────────────────────────────────

    [Theory]
    [MemberData(nameof(SampleIfcFiles))]
    public async Task ConversionService_ReceivesCorrectSourcePath_ForEachSample(string fileName)
    {
        var sourceFilePath = Path.Combine(SamplesDirectory, fileName);
        var expectedOutput = Path.ChangeExtension(sourceFilePath, ".xkt");

        var serviceMock = new Mock<IIfcConversionService>();
        serviceMock
            .Setup(s => s.ConvertAsync(
                sourceFilePath,
                ConversionFormat.XKT,
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOutput);

        var result = await serviceMock.Object.ConvertAsync(
            sourceFilePath, ConversionFormat.XKT);

        result.Should().Be(expectedOutput);
        Path.GetExtension(result).Should().Be(".xkt",
            because: "the converted output must carry the .xkt extension");

        serviceMock.Verify(s => s.ConvertAsync(
            sourceFilePath, ConversionFormat.XKT, null, default), Times.Once);
    }

    [Fact]
    public async Task ConversionService_ReportsXktFormatAsSupported()
    {
        var serviceMock = new Mock<IIfcConversionService>();
        serviceMock
            .Setup(s => s.IsConversionSupportedAsync(ConversionFormat.XKT))
            .ReturnsAsync(true);

        var supported = await serviceMock.Object
            .IsConversionSupportedAsync(ConversionFormat.XKT);

        supported.Should().BeTrue();
    }

    // ── Full pipeline: create → process → complete ───────────────────────────

    [Theory]
    [MemberData(nameof(SampleIfcFiles))]
    public async Task Pipeline_ConvertsEachSampleFile_ToXkt_Successfully(string fileName)
    {
        var sourceFilePath = Path.Combine(SamplesDirectory, fileName);
        var outputPath = Path.ChangeExtension(sourceFilePath, ".xkt");
        var fileSize = new FileInfo(sourceFilePath).Length;

        var model = IfcModel.Create(fileName, sourceFilePath, fileSize);

        var modelRepoMock = new Mock<IIfcModelRepository>();
        modelRepoMock
            .Setup(r => r.GetByIdAsync(model.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);

        var jobRepoMock = new Mock<IConversionJobRepository>();
        jobRepoMock
            .Setup(r => r.AddAsync(It.IsAny<ConversionJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversionJob j, CancellationToken _) => j);

        var serviceMock = new Mock<IIfcConversionService>();
        serviceMock
            .Setup(s => s.ConvertAsync(
                sourceFilePath,
                ConversionFormat.XKT,
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(outputPath);

        var handler = new CreateConversionJobCommandHandler(
            modelRepoMock.Object, jobRepoMock.Object);

        // Step 1 – enqueue job
        var command = new CreateConversionJobCommand(model.Id, ConversionFormat.XKT);
        var jobDto = await handler.Handle(command, default);
        jobDto.Status.Should().Be(ConversionStatus.Queued);

        // Step 2 – simulate background worker picking up the job
        var job = model.ConversionJobs.Single();
        job.StartProcessing();
        job.Status.Should().Be(ConversionStatus.Processing);
        job.StartedAt.Should().NotBeNull();

        // Step 3 – simulate conversion
        var xktPath = await serviceMock.Object.ConvertAsync(
            model.OriginalFilePath, ConversionFormat.XKT);

        job.Complete(xktPath);

        // Assert final state
        job.Status.Should().Be(ConversionStatus.Completed);
        job.OutputFilePath.Should().EndWith(".xkt");
        job.ProgressPercentage.Should().Be(100);
        job.CompletedAt.Should().NotBeNull();
        job.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(SampleIfcFiles))]
    public async Task Pipeline_WhenConversionFails_MarksJobAsFailed(string fileName)
    {
        var sourceFilePath = Path.Combine(SamplesDirectory, fileName);
        var fileSize = new FileInfo(sourceFilePath).Length;

        var model = IfcModel.Create(fileName, sourceFilePath, fileSize);

        var modelRepoMock = new Mock<IIfcModelRepository>();
        modelRepoMock
            .Setup(r => r.GetByIdAsync(model.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);

        var jobRepoMock = new Mock<IConversionJobRepository>();
        jobRepoMock
            .Setup(r => r.AddAsync(It.IsAny<ConversionJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversionJob j, CancellationToken _) => j);

        var serviceMock = new Mock<IIfcConversionService>();
        serviceMock
            .Setup(s => s.ConvertAsync(
                It.IsAny<string>(),
                It.IsAny<ConversionFormat>(),
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ifc-convert exited with code 1"));

        var handler = new CreateConversionJobCommandHandler(
            modelRepoMock.Object, jobRepoMock.Object);

        var jobDto = await handler.Handle(
            new CreateConversionJobCommand(model.Id, ConversionFormat.XKT), default);

        var job = model.ConversionJobs.Single();
        job.StartProcessing();

        try
        {
            await serviceMock.Object.ConvertAsync(
                model.OriginalFilePath, ConversionFormat.XKT);
        }
        catch (InvalidOperationException ex)
        {
            job.Fail(ex.Message);
        }

        job.Status.Should().Be(ConversionStatus.Failed);
        job.ErrorMessage.Should().Contain("ifc-convert");
        job.CompletedAt.Should().NotBeNull();
        job.OutputFilePath.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(SampleIfcFiles))]
    public async Task Pipeline_ReportsProgressDuringConversion(string fileName)
    {
        var sourceFilePath = Path.Combine(SamplesDirectory, fileName);
        var outputPath = Path.ChangeExtension(sourceFilePath, ".xkt");
        var fileSize = new FileInfo(sourceFilePath).Length;

        var reportedProgress = new List<int>();
        var model = IfcModel.Create(fileName, sourceFilePath, fileSize);

        var serviceMock = new Mock<IIfcConversionService>();
        serviceMock
            .Setup(s => s.ConvertAsync(
                sourceFilePath,
                ConversionFormat.XKT,
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, ConversionFormat, IProgress<int>?, CancellationToken>(
                (_, _, progress, _) =>
                {
                    progress?.Report(25);
                    progress?.Report(50);
                    progress?.Report(75);
                    progress?.Report(100);
                })
            .ReturnsAsync(outputPath);

        var job = model.CreateConversionJob(ConversionFormat.XKT);
        job.StartProcessing();

        var progressReporter = new SyncProgress<int>(p =>
        {
            reportedProgress.Add(p);
            job.UpdateProgress(p);
        });

        await serviceMock.Object.ConvertAsync(
            sourceFilePath, ConversionFormat.XKT, progressReporter);
        job.Complete(outputPath);

        reportedProgress.Should().ContainInOrder(25, 50, 75, 100);
        job.ProgressPercentage.Should().Be(100);
        job.Status.Should().Be(ConversionStatus.Completed);
    }
}
