using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Domain.Enums;
using System.Diagnostics;

namespace BombasticIFC.Infrastructure.Services;

/// <summary>
/// Converts IFC files to XKT using convert2xkt, which reads IFC directly via web-ifc.
/// </summary>
public class IfcConversionService : IIfcConversionService
{
    private readonly string _storagePath;

    public IfcConversionService(string storagePath)
    {
        _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));

        if (!Directory.Exists(_storagePath))
            Directory.CreateDirectory(_storagePath);
    }

    public async Task<string> ConvertAsync(
        string sourceFilePath,
        ConversionFormat targetFormat,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException($"Source IFC file not found: {sourceFilePath}");

        var xktPath = Path.Combine(_storagePath, $"{Guid.NewGuid():N}.xkt");

        // Single step: IFC → XKT via xeokit-convert (uses web-ifc internally)
        await RunProcessAsync(
            "xeokit-convert",
            $"-s \"{sourceFilePath}\" -o \"{xktPath}\"",
            cancellationToken);

        progress?.Report(100);

        return xktPath;
    }

    public Task<bool> IsConversionSupportedAsync(ConversionFormat format)
        => Task.FromResult(format == ConversionFormat.XKT);

    private static async Task RunProcessAsync(string command, string arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var stderr = await stderrTask;
            throw new InvalidOperationException(
                $"'{command}' exited with code {process.ExitCode}. stderr: {stderr}");
        }
    }
}
