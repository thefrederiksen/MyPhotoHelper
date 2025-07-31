using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyPhotoHelper.Models;

public partial class tbl_image_analysis
{
    [Key]
    public int ImageId { get; set; }

    public string? ImageCategory { get; set; }

    public string? PhotoSubcategory { get; set; }

    public string? AIAnalysisJson { get; set; }

    public string? AIDescription { get; set; }

    public DateTime? AIAnalyzedAt { get; set; }

    public string? AIModelUsed { get; set; }

    public string? AIKeywords { get; set; }

    public virtual tbl_images Image { get; set; } = null!;
}
