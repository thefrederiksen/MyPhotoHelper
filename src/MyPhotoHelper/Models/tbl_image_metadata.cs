using System;
using System.Collections.Generic;

namespace MyPhotoHelper.Models;

public partial class tbl_image_metadata
{
    public int ImageId { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public DateTime? DateTaken { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? LocationName { get; set; }

    public virtual tbl_images Image { get; set; } = null!;
}
