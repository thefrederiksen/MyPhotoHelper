## Feature Request: Automatic Startup with Settings Control

### Overview
Add functionality to automatically start MyPhotoHelper when Windows starts, with a user-configurable setting to enable/disable this feature. Include a settings page with a checkbox to control the startup behavior.

### Requirements

#### 1. Automatic Startup Implementation
- [ ] Add application to Windows startup registry
- [ ] Implement startup registration/unregistration logic
- [ ] Handle startup with minimized state
- [ ] Ensure proper startup order and dependencies
- [ ] Handle startup failures gracefully
- [ ] Support for both current user and all users startup

#### 2. Settings Page Development
- [ ] Create new Settings page/form in the application
- [ ] Add checkbox for "Start with Windows" option
- [ ] Default setting: Enabled (checked)
- [ ] Real-time application of setting changes
- [ ] Visual feedback when setting is changed
- [ ] Settings persistence across app restarts

#### 3. Startup Management Service
- [ ] Create StartupService to handle registry operations
- [ ] Implement startup status checking
- [ ] Add startup registration validation
- [ ] Handle startup path management
- [ ] Support for startup arguments/parameters
- [ ] Error handling and logging

#### 4. User Experience Features
- [ ] Settings page accessible from main menu/navigation
- [ ] Clear labeling and descriptions for startup options
- [ ] Immediate feedback when setting is toggled
- [ ] Confirmation dialog for startup changes
- [ ] Help text explaining startup behavior
- [ ] Visual indicators for current startup status

### Technical Implementation Details

#### Startup Service Implementation
```csharp
public class StartupService
{
    private const string StartupKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string AppName = "MyPhotoHelper";

    public bool IsStartupEnabled()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupKey))
            {
                return key?.GetValue(AppName) != null;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error checking startup status: {ex.Message}");
            return false;
        }
    }

    public bool SetStartupEnabled(bool enabled)
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupKey, true))
            {
                if (enabled)
                {
                    var exePath = Process.GetCurrentProcess().MainModule.FileName;
                    key.SetValue(AppName, $"\"{exePath}\" --startup");
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error setting startup: {ex.Message}");
            return false;
        }
    }
}
```

#### Settings Page Implementation
```csharp
public partial class SettingsForm : Form
{
    private readonly StartupService _startupService;
    private readonly IConfiguration _configuration;

    public SettingsForm(StartupService startupService, IConfiguration configuration)
    {
        InitializeComponent();
        _startupService = startupService;
        _configuration = configuration;
        LoadSettings();
    }

    private void LoadSettings()
    {
        // Load startup setting
        chkStartWithWindows.Checked = _startupService.IsStartupEnabled();
        
        // Load other settings as needed
        // chkAutoUpdate.Checked = _configuration.GetValue<bool>("AutoUpdate", true);
    }

    private void chkStartWithWindows_CheckedChanged(object sender, EventArgs e)
    {
        bool enabled = chkStartWithWindows.Checked;
        bool success = _startupService.SetStartupEnabled(enabled);
        
        if (success)
        {
            lblStartupStatus.Text = enabled ? "Startup enabled" : "Startup disabled";
            lblStartupStatus.ForeColor = enabled ? Color.Green : Color.Gray;
        }
        else
        {
            MessageBox.Show("Failed to update startup setting. Please try again.", 
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            chkStartWithWindows.Checked = !enabled; // Revert the change
        }
    }
}
```

#### Program.cs Startup Handling
```csharp
[STAThread]
public static void Main(string[] args)
{
    // Check if started from Windows startup
    bool isStartup = args.Contains("--startup");
    
    if (isStartup)
    {
        // Start minimized or in system tray
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        
        var mainForm = new MainForm();
        mainForm.WindowState = FormWindowState.Minimized;
        mainForm.ShowInTaskbar = false;
        
        Application.Run(mainForm);
    }
    else
    {
        // Normal startup
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
```

### Settings Page UI Design

#### Layout Structure
```xml
<Form Text="Settings" Size="500,400" StartPosition="CenterParent">
    <TabControl Dock="Fill">
        <TabPage Text="General">
            <GroupBox Text="Startup Options">
                <CheckBox Name="chkStartWithWindows" Text="Start MyPhotoHelper when Windows starts" />
                <Label Name="lblStartupStatus" Text="Startup enabled" ForeColor="Green" />
                <Label Text="When enabled, MyPhotoHelper will automatically start when you log into Windows." ForeColor="Gray" />
            </GroupBox>
            
            <GroupBox Text="Update Options">
                <CheckBox Name="chkAutoUpdate" Text="Automatically check for updates" />
                <CheckBox Name="chkAutoInstall" Text="Automatically install updates" />
            </GroupBox>
        </TabPage>
        
        <TabPage Text="Advanced">
            <!-- Additional settings can be added here -->
        </TabPage>
    </TabControl>
    
    <Panel Dock="Bottom" Height="50">
        <Button Name="btnOK" Text="OK" DialogResult="OK" />
        <Button Name="btnCancel" Text="Cancel" DialogResult="Cancel" />
    </Panel>
</Form>
```

### Configuration and Persistence

#### App Settings Structure
```json
{
  "StartupSettings": {
    "StartWithWindows": true,
    "StartMinimized": false,
    "ShowInTaskbar": true
  },
  "UpdateSettings": {
    "AutoCheck": true,
    "AutoInstall": false,
    "CheckInterval": "06:00:00"
  }
}
```

#### Settings Service
```csharp
public class SettingsService
{
    private readonly IConfiguration _configuration;
    private readonly StartupService _startupService;

    public SettingsService(IConfiguration configuration, StartupService startupService)
    {
        _configuration = configuration;
        _startupService = startupService;
    }

    public bool GetStartupSetting()
    {
        return _configuration.GetValue<bool>("StartupSettings:StartWithWindows", true);
    }

    public void SetStartupSetting(bool enabled)
    {
        // Update configuration
        // Update registry
        _startupService.SetStartupEnabled(enabled);
    }
}
```

### User Experience Flow

#### First Installation
1. App installs with startup enabled by default
2. User sees notification about startup behavior
3. Settings page accessible to modify behavior
4. Clear explanation of what startup means

#### Settings Management
1. User opens Settings from main menu
2. Sees current startup status with visual indicator
3. Can toggle startup setting with immediate feedback
4. Changes applied immediately to registry
5. Confirmation of successful change

#### Startup Behavior
1. App starts automatically on Windows login
2. Starts in background/minimized state
3. No intrusive startup notifications
4. Normal functionality available immediately

### Security and Best Practices

#### Security Considerations
- [ ] Use current user registry only (no admin rights needed)
- [ ] Validate startup path before registration
- [ ] Sanitize startup arguments
- [ ] Handle registry access permissions gracefully
- [ ] Log all startup-related operations

#### Error Handling
- [ ] Handle registry access failures
- [ ] Provide user-friendly error messages
- [ ] Fallback behavior when startup fails
- [ ] Recovery options for corrupted settings
- [ ] Validation of startup path existence

#### Testing Requirements
- [ ] Test startup on fresh Windows installation
- [ ] Test startup with different user accounts
- [ ] Test startup with antivirus software
- [ ] Test startup with Windows updates
- [ ] Test startup cancellation and recovery
- [ ] Test settings persistence across reboots
- [ ] Test startup with network drives unavailable

### Integration Points

#### Main Application Integration
- [ ] Add Settings menu item to main form
- [ ] Integrate with existing navigation system
- [ ] Handle settings changes in real-time
- [ ] Update UI based on current settings
- [ ] Persist settings across application sessions

#### Squirrel Integration (Future)
- [ ] Handle startup setting during app updates
- [ ] Preserve startup setting across version updates
- [ ] Update startup path after app location changes
- [ ] Handle startup in portable mode

### Accessibility and Localization

#### Accessibility Features
- [ ] Keyboard navigation support
- [ ] Screen reader compatibility
- [ ] High contrast mode support
- [ ] Proper tab order and focus management
- [ ] Clear visual indicators for settings state

#### Localization Support
- [ ] Localizable strings for all UI text
- [ ] Support for different date/time formats
- [ ] Right-to-left language support
- [ ] Cultural considerations for startup behavior

### Performance Considerations

#### Startup Performance
- [ ] Minimize startup time impact
- [ ] Lazy loading of non-critical components
- [ ] Background initialization of services
- [ ] Startup progress indicators
- [ ] Timeout handling for slow startup

#### Memory Management
- [ ] Efficient settings storage and retrieval
- [ ] Proper disposal of registry handles
- [ ] Memory cleanup on settings changes
- [ ] Resource monitoring during startup

### Future Enhancements

#### Phase 2 Features
- [ ] Startup delay options (start after X seconds)
- [ ] Conditional startup (only when certain conditions met)
- [ ] Startup performance monitoring
- [ ] Startup failure reporting
- [ ] Advanced startup scheduling

#### Advanced Options
- [ ] Multiple startup profiles
- [ ] Startup with specific parameters
- [ ] Startup priority management
- [ ] Startup dependency management
- [ ] Startup health monitoring

### Priority and Impact

**Medium Priority** - This feature will improve user convenience by ensuring MyPhotoHelper is always available when needed, while providing user control over the behavior.

**User Impact**
- Improved user convenience and accessibility
- Reduced manual startup effort
- Consistent application availability
- User control over startup behavior
- Professional application experience

### Related Issues
- Integrates with Squirrel.Windows deployment system
- May affect application startup performance
- Requires careful testing with existing features
- Could impact system resource usage

### Acceptance Criteria
- [ ] Application starts automatically with Windows by default
- [ ] Settings page with startup checkbox is accessible
- [ ] Startup setting can be toggled on/off
- [ ] Setting persists across application restarts
- [ ] Visual feedback shows current startup status
- [ ] No admin rights required for startup management
- [ ] Graceful handling of startup failures
- [ ] Clear user interface and help text
- [ ] Comprehensive testing completed
- [ ] Documentation updated for users 