using BombasticIFC.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BombasticIFC.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for IfcModel
/// </summary>
public class IfcModelConfiguration : IEntityTypeConfiguration<IfcModel>
{
    public void Configure(EntityTypeBuilder<IfcModel> builder)
    {
        builder.ToTable("IfcModels");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(m => m.OriginalFilePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(m => m.FileSizeBytes)
            .IsRequired();

        builder.Property(m => m.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.OwnsOne(m => m.Metadata, metadata =>
        {
            metadata.Property(md => md.IfcSchema).HasMaxLength(50);
            metadata.Property(md => md.ProjectName).HasMaxLength(255);
            metadata.Property(md => md.ApplicationName).HasMaxLength(255);
            metadata.Property(md => md.NumberOfElements);
            metadata.OwnsOne(md => md.ElementTypeCounts);
        });

        builder.HasMany(m => m.ConversionJobs)
            .WithOne(j => j.Model)
            .HasForeignKey(j => j.ModelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Versions)
            .WithOne(v => v.Model)
            .HasForeignKey(v => v.ModelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.FileName);
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.CreatedAt);
    }
}
