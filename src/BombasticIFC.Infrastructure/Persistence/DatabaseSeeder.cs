using BombasticIFC.Domain.Entities;
using BombasticIFC.Domain.Enums;
using BombasticIFC.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BombasticIFC.Infrastructure.Persistence;

/// <summary>
/// Seeds the database with sample data (Duplex.ifc demo model)
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, string storagePath, ILogger logger)
    {
        // Only seed if the Duplex sample doesn't already exist
        if (await context.IfcModels.AnyAsync(m => m.FileName == "Duplex.ifc"))
            return;

        logger.LogInformation("Seeding database with sample Duplex.ifc model...");

        // Copy sample XKT from seed-data to storage if not already there
        var samplesDir = Path.Combine(storagePath, "samples");
        Directory.CreateDirectory(samplesDir);
        var sampleXktPath = Path.Combine(samplesDir, "Duplex.xkt");
        var seedSourcePath = Path.Combine(AppContext.BaseDirectory, "seed-data", "Duplex.xkt");

        if (!File.Exists(sampleXktPath) && File.Exists(seedSourcePath))
        {
            File.Copy(seedSourcePath, sampleXktPath);
            logger.LogInformation("Copied sample Duplex.xkt to {Path}", sampleXktPath);
        }

        var sampleIfcPath = Path.Combine(storagePath, "samples", "Duplex.ifc");

        var model = IfcModel.Create(
            fileName: "Duplex.ifc",
            originalFilePath: sampleIfcPath,
            fileSizeBytes: 495_000);

        model.UpdateStatus(ModelStatus.Ready);
        model.UpdateMetadata(ModelMetadata.Create(
            ifcSchema: "IFC2X3",
            projectName: "Duplex Apartment",
            applicationName: "Autodesk Revit 2024",
            numberOfElements: 286));

        // Create a completed XKT conversion job through the model's domain method
        var job = model.CreateConversionJob(ConversionFormat.XKT);
        job.StartProcessing();
        job.Complete(sampleXktPath);

        context.IfcModels.Add(model);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded sample model {ModelId} with completed conversion job {JobId}",
            model.Id, job.Id);
    }
}
