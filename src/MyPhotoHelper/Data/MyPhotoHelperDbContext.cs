using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Data;

public partial class MyPhotoHelperDbContext : DbContext
{
    public MyPhotoHelperDbContext(DbContextOptions<MyPhotoHelperDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<tbl_app_settings> tbl_app_settings { get; set; }

    public virtual DbSet<tbl_image_analysis> tbl_image_analysis { get; set; }

    public virtual DbSet<tbl_image_metadata> tbl_image_metadata { get; set; }

    public virtual DbSet<tbl_images> tbl_images { get; set; }

    public virtual DbSet<tbl_scan_directory> tbl_scan_directory { get; set; }

    public virtual DbSet<tbl_version> tbl_version { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<tbl_app_settings>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.DateCreated).HasColumnType("DATETIME");
            entity.Property(e => e.DateModified).HasColumnType("DATETIME");
            entity.Property(e => e.LastScanDate).HasColumnType("DATETIME");
            entity.Property(e => e.RunOnWindowsStartup).HasDefaultValue(0);
        });

        modelBuilder.Entity<tbl_image_analysis>(entity =>
        {
            entity.HasKey(e => e.ImageId);

            entity.HasIndex(e => e.ImageCategory, "IX_tbl_image_analysis_ImageCategory");

            entity.Property(e => e.ImageId).ValueGeneratedNever();
            entity.Property(e => e.AIAnalyzedAt).HasColumnType("DATETIME");

            entity.HasOne(d => d.Image).WithOne(p => p.tbl_image_analysis).HasForeignKey<tbl_image_analysis>(d => d.ImageId);
        });

        modelBuilder.Entity<tbl_image_metadata>(entity =>
        {
            entity.HasKey(e => e.ImageId);

            entity.HasIndex(e => e.ImageId, "IX_tbl_image_metadata_ImageId");

            entity.HasIndex(e => new { e.Latitude, e.Longitude }, "IX_tbl_image_metadata_Location");

            entity.Property(e => e.ImageId).ValueGeneratedNever();
            entity.Property(e => e.DateDigitized).HasColumnType("DATETIME");
            entity.Property(e => e.DateModified).HasColumnType("DATETIME");
            entity.Property(e => e.DateTaken).HasColumnType("DATETIME");

            entity.HasOne(d => d.Image).WithOne(p => p.tbl_image_metadata).HasForeignKey<tbl_image_metadata>(d => d.ImageId);
        });

        modelBuilder.Entity<tbl_images>(entity =>
        {
            entity.HasKey(e => e.ImageId);

            entity.HasIndex(e => new { e.RelativePath, e.ScanDirectoryId }, "IX_tbl_images_RelativePath_ScanDirectoryId").IsUnique();

            entity.HasIndex(e => e.DateCreated, "IX_tbl_images_DateCreated");

            entity.HasIndex(e => new { e.FileExists, e.IsDeleted }, "IX_tbl_images_FileExists_IsDeleted");

            entity.HasIndex(e => e.FileHash, "IX_tbl_images_FileHash");

            entity.HasIndex(e => e.IsDeleted, "IX_tbl_images_IsDeleted");

            entity.HasIndex(e => e.ScanDirectoryId, "IX_tbl_images_ScanDirectoryId");

            entity.Property(e => e.DateCreated).HasColumnType("DATETIME");
            entity.Property(e => e.DateDeleted).HasColumnType("DATETIME");
            entity.Property(e => e.DateModified).HasColumnType("DATETIME");
            entity.Property(e => e.FileExists).HasDefaultValue(1);

            entity.HasOne(d => d.ScanDirectory).WithMany(p => p.tbl_images).HasForeignKey(d => d.ScanDirectoryId);
        });

        modelBuilder.Entity<tbl_scan_directory>(entity =>
        {
            entity.HasKey(e => e.ScanDirectoryId);

            entity.HasIndex(e => e.DirectoryPath, "IX_tbl_scan_directory_DirectoryPath").IsUnique();

            entity.HasIndex(e => e.DirectoryPath, "IX_tbl_scan_directory_DirectoryPath");

            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("DATETIME");
        });

        modelBuilder.Entity<tbl_version>(entity =>
        {
            entity.HasNoKey();
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
