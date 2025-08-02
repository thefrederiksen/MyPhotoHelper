using System;
using System.Collections.Generic;

namespace MyPhotoHelper.Models;

public partial class tbl_app_settings
{
    public string SettingName { get; set; } = null!;

    public string SettingType { get; set; } = null!;

    public string SettingValue { get; set; } = null!;

    public DateTime? CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }
}
