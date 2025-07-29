using System;
using System.Collections.Generic;

namespace MyPhotoHelper.Models;

public partial class tbl_scan_directory
{
    public int ScanDirectoryId { get; set; }

    public string DirectoryPath { get; set; } = null!;

    public DateTime DateCreated { get; set; }

    public virtual ICollection<tbl_images> tbl_images { get; set; } = new List<tbl_images>();
}
