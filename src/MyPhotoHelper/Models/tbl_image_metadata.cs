using System;
using System.Collections.Generic;

namespace MyPhotoHelper.Models;

public partial class tbl_image_metadata
{
    public int ImageId { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public string? ColorSpace { get; set; }

    public int? BitDepth { get; set; }

    public string? Orientation { get; set; }

    public double? ResolutionX { get; set; }

    public double? ResolutionY { get; set; }

    public string? ResolutionUnit { get; set; }

    public DateTime? DateTaken { get; set; }

    public DateTime? DateDigitized { get; set; }

    public DateTime? DateModified { get; set; }

    public string? TimeZone { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public double? Altitude { get; set; }

    public string? GPSDirection { get; set; }

    public double? GPSSpeed { get; set; }

    public string? GPSProcessingMethod { get; set; }

    public string? LocationName { get; set; }

    public string? CameraMake { get; set; }

    public string? CameraModel { get; set; }

    public string? CameraSerial { get; set; }

    public string? LensModel { get; set; }

    public string? LensMake { get; set; }

    public string? LensSerial { get; set; }

    public double? FocalLength { get; set; }

    public double? FocalLength35mm { get; set; }

    public string? FNumber { get; set; }

    public string? ExposureTime { get; set; }

    public int? ISO { get; set; }

    public string? ExposureMode { get; set; }

    public string? ExposureProgram { get; set; }

    public string? MeteringMode { get; set; }

    public string? Flash { get; set; }

    public string? WhiteBalance { get; set; }

    public string? SceneCaptureType { get; set; }

    public string? Software { get; set; }

    public string? ProcessingSoftware { get; set; }

    public string? Artist { get; set; }

    public string? Copyright { get; set; }

    public string? ColorProfile { get; set; }

    public double? ExposureBias { get; set; }

    public double? MaxAperture { get; set; }

    public string? SubjectDistance { get; set; }

    public string? LightSource { get; set; }

    public string? SensingMethod { get; set; }

    public string? FileSource { get; set; }

    public string? SceneType { get; set; }

    public string? ImageDescription { get; set; }

    public string? UserComment { get; set; }

    public string? Keywords { get; set; }

    public string? Subject { get; set; }

    public virtual tbl_images Image { get; set; } = null!;
}
