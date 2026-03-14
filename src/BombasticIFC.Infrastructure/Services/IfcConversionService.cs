using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace BombasticIFC.Infrastructure.Services;

/// <summary>
/// Converts IFC files to XKT using xeokit-convert (web-ifc backend).
/// </summary>
public class IfcConversionService : IIfcConversionService
{
    private readonly string _storagePath;
    private readonly ILogger<IfcConversionService> _logger;
    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromMinutes(15);
    private static readonly Regex FileSchemaRegex = new(@"FILE_SCHEMA\s*\(\s*\('(?<schema>[^']+)'\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IfcConversionService(string storagePath, ILogger<IfcConversionService> logger)
    {
        _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!Directory.Exists(_storagePath))
            Directory.CreateDirectory(_storagePath);
    }

    public async Task<string> ConvertAsync(
        string sourceFilePath,
        ConversionFormat targetFormat,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (targetFormat != ConversionFormat.XKT)
            throw new NotSupportedException($"Conversion target format '{targetFormat}' is not supported. Only XKT is currently supported.");

        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException($"Source IFC file not found: {sourceFilePath}");

        var fileInfo = new FileInfo(sourceFilePath);
        if (fileInfo.Length <= 0)
            throw new InvalidDataException($"Source IFC file is empty: {sourceFilePath}");

        await ValidateIfcHeaderAsync(sourceFilePath, cancellationToken);

        var xktPath = Path.Combine(_storagePath, $"{Guid.NewGuid():N}.xkt");
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting IFC conversion. Source={SourcePath}, Target={TargetPath}, SizeBytes={SizeBytes}",
            sourceFilePath,
            xktPath,
            fileInfo.Length);

        try
        {
            await RunProcessAsync(
                "xeokit-convert",
                ["-s", sourceFilePath, "-o", xktPath],
                ConversionTimeout,
                cancellationToken);

            progress?.Report(100);

            stopwatch.Stop();
            _logger.LogInformation(
                "IFC conversion completed successfully. Source={SourcePath}, Target={TargetPath}, DurationMs={DurationMs}",
                sourceFilePath,
                xktPath,
                stopwatch.ElapsedMilliseconds);

            return xktPath;
        }
        catch
        {
            await TryDeletePartialOutputAsync(xktPath);
            throw;
        }
    }

    public Task<bool> IsConversionSupportedAsync(ConversionFormat format)
        => Task.FromResult(format == ConversionFormat.XKT);

    private async Task RunProcessAsync(
        string command,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = psi };
        if (!process.Start())
            throw new InvalidOperationException($"Failed to start conversion process '{command}'.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);

            if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                throw new TimeoutException($"Conversion process '{command}' timed out after {timeout.TotalMinutes:F0} minutes.");

            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        _logger.LogInformation(
            "Conversion process finished. Command={Command}, ExitCode={ExitCode}",
            command,
            process.ExitCode);

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            _logger.LogInformation("Conversion stdout: {Stdout}", LimitForLog(stdout));
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            _logger.LogWarning("Conversion stderr: {Stderr}", LimitForLog(stderr));
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'{command}' exited with code {process.ExitCode}. stderr: {LimitForLog(stderr)}");
        }
    }

    private static async Task ValidateIfcHeaderAsync(string sourceFilePath, CancellationToken cancellationToken)
    {
        const int maxHeaderBytes = 16 * 1024;
        var buffer = new byte[maxHeaderBytes];

        await using var stream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, maxHeaderBytes), cancellationToken);
        var header = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        if (!header.Contains("ISO-10303-21", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Input file is not a valid STEP/IFC file: missing ISO-10303-21 header.");

        if (!header.Contains("FILE_SCHEMA", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Input IFC header is invalid: missing FILE_SCHEMA declaration.");

        var schemaMatch = FileSchemaRegex.Match(header);
        if (!schemaMatch.Success)
            throw new InvalidDataException("Input IFC header is invalid: schema declaration could not be parsed.");

        var schema = schemaMatch.Groups["schema"].Value;
        if (!schema.Contains("IFC", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Input file schema '{schema}' is not an IFC schema.");
    }

    private async Task TryDeletePartialOutputAsync(string outputPath)
    {
        try
        {
            if (File.Exists(outputPath))
            {
                await Task.Run(() => File.Delete(outputPath));
                _logger.LogInformation("Deleted partial conversion output: {OutputPath}", outputPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete partial output file: {OutputPath}", outputPath);
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort only.
        }
    }

    private static string LimitForLog(string text)
    {
        const int max = 4000;
        if (text.Length <= max)
            return text;

        return text[..max] + "... [truncated]";
    }
}
