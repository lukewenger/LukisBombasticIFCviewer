using BombasticIFC.Application.Common.Interfaces;

namespace BombasticIFC.Infrastructure.Services;

/// <summary>
/// File storage service implementation using local file system
/// </summary>
public class FileStorageService : IFileStorageService
{
    private readonly string _storagePath;

    public FileStorageService(string storagePath)
    {
        _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
        
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        var filePath = Path.Combine(_storagePath, uniqueFileName);

        using var fileStreamOutput = File.Create(filePath);
        await fileStream.CopyToAsync(fileStreamOutput, cancellationToken);

        return filePath;
    }

    public async Task<Stream> GetFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!await FileExistsAsync(filePath, cancellationToken))
            throw new FileNotFoundException($"File not found: {filePath}");

        return File.OpenRead(filePath);
    }

    public Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    public Task<bool> FileExistsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(filePath));
    }

    public Task<long> GetFileSizeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(filePath);
        return Task.FromResult(fileInfo.Length);
    }
}
