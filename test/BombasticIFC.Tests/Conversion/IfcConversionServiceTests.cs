using BombasticIFC.Domain.Enums;
using BombasticIFC.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BombasticIFC.Tests.Conversion;

public class IfcConversionServiceTests
{
    [Fact]
    public async Task ConvertAsync_ShouldRejectUnsupportedTargetFormat()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var inputPath = Path.Combine(tempDir, "model.ifc");
            await File.WriteAllTextAsync(inputPath, "ISO-10303-21;\nHEADER;\nFILE_SCHEMA(('IFC4'));\nENDSEC;\n");

            var service = new IfcConversionService(tempDir, NullLogger<IfcConversionService>.Instance);

            var act = () => service.ConvertAsync(inputPath, ConversionFormat.GLTF);

            await act.Should().ThrowAsync<NotSupportedException>();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_ShouldFailWhenIsoHeaderMissing()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var inputPath = Path.Combine(tempDir, "invalid.ifc");
            await File.WriteAllTextAsync(inputPath, "HEADER;\nFILE_SCHEMA(('IFC4'));\n");

            var service = new IfcConversionService(tempDir, NullLogger<IfcConversionService>.Instance);

            var act = () => service.ConvertAsync(inputPath, ConversionFormat.XKT);

            await act.Should().ThrowAsync<InvalidDataException>()
                .WithMessage("*ISO-10303-21*");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_ShouldFailWhenSchemaIsMissing()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var inputPath = Path.Combine(tempDir, "invalid.ifc");
            await File.WriteAllTextAsync(inputPath, "ISO-10303-21;\nHEADER;\nENDSEC;\n");

            var service = new IfcConversionService(tempDir, NullLogger<IfcConversionService>.Instance);

            var act = () => service.ConvertAsync(inputPath, ConversionFormat.XKT);

            await act.Should().ThrowAsync<InvalidDataException>()
                .WithMessage("*FILE_SCHEMA*");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task IsConversionSupportedAsync_ShouldOnlySupportXkt()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var service = new IfcConversionService(tempDir, NullLogger<IfcConversionService>.Instance);

            var xktSupported = await service.IsConversionSupportedAsync(ConversionFormat.XKT);
            var glbSupported = await service.IsConversionSupportedAsync(ConversionFormat.GLB);

            xktSupported.Should().BeTrue();
            glbSupported.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bombastic-ifc-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
