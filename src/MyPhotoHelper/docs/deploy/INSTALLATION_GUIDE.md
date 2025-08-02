# MyPhotoHelper Installation Guide

## Download and Install

### First-Time Installation

1. **Download the installer**
   - Go to the [Latest Release](https://github.com/thefrederiksen/MyPhotoHelper/releases/latest)
   - Download `MyPhotoHelper-Setup.exe`

2. **Run the installer**
   - Double-click `MyPhotoHelper-Setup.exe`
   - If Windows SmartScreen appears, click "More info" then "Run anyway"
   - The installer will:
     - Download and install MyPhotoHelper
     - Create desktop and Start Menu shortcuts
     - Register the application for automatic updates

3. **Launch the application**
   - Use the desktop shortcut or Start Menu entry
   - The application will open in your default browser at `http://localhost:5113`
   - A system tray icon will appear for easy access

### System Requirements

- **Operating System**: Windows 10 or later (64-bit)
- **Memory**: 4GB RAM minimum, 8GB recommended
- **Storage**: 500MB for application, additional space for photo database
- **.NET Runtime**: Included with installer (self-contained)

## Features After Installation

### Automatic Updates
- MyPhotoHelper checks for updates every 6 hours
- When an update is available, you'll see a notification in the app
- Click "Restart Now" to apply the update immediately
- Updates are downloaded in the background without interrupting your work

### System Tray Integration
- Right-click the system tray icon for quick access to:
  - Open MyPhotoHelper
  - Check for updates
  - Exit application

### Data Storage
- Photos remain in their original locations
- Application data stored in: `%APPDATA%\MyPhotoHelper\`
- Database location: `%APPDATA%\MyPhotoHelper\Database\`

## Troubleshooting

### Installation Issues

**"Windows protected your PC" message**
- This is Windows SmartScreen protecting you from unsigned apps
- Click "More info" then "Run anyway" to proceed
- The app will be signed in future releases

**Installation fails with error**
- Ensure you have administrator privileges
- Check that port 5113 is not in use
- Temporarily disable antivirus during installation

### Running Issues

**Application won't start**
- Check if MyPhotoHelper is already running (system tray)
- Restart your computer and try again
- Check logs at: `%APPDATA%\MyPhotoHelper\Logs\`

**Browser doesn't open automatically**
- Manually navigate to: `http://localhost:5113`
- Check your default browser settings
- The app runs even if the browser doesn't open

### Update Issues

**Updates fail to download**
- Check your internet connection
- Ensure GitHub.com is accessible
- Check firewall settings for MyPhotoHelper.exe
- Manually download updates from [Releases](https://github.com/thefrederiksen/MyPhotoHelper/releases)

## Uninstalling

1. **Using Windows Settings**
   - Open Windows Settings > Apps
   - Find "MyPhotoHelper" in the list
   - Click Uninstall

2. **Using Control Panel**
   - Open Control Panel > Programs and Features
   - Find "MyPhotoHelper"
   - Click Uninstall

The uninstaller will:
- Remove the application files
- Remove shortcuts
- Keep your photo database and settings (in %APPDATA%)

To completely remove all data:
- After uninstalling, manually delete: `%APPDATA%\MyPhotoHelper\`

## Manual Update Installation

If automatic updates aren't working:

1. Download the latest `MyPhotoHelper-Setup.exe` from [Releases](https://github.com/thefrederiksen/MyPhotoHelper/releases)
2. Close MyPhotoHelper (right-click system tray icon > Exit)
3. Run the new Setup.exe - it will update your existing installation
4. Your settings and database will be preserved

## Command Line Options

MyPhotoHelper supports these command line arguments:

- `--minimized` - Start minimized to system tray
- `--test-gps` - Run GPS metadata test

Example: `MyPhotoHelper.exe --minimized`

## Getting Help

- **Documentation**: Check the docs folder in the repository
- **Issues**: Report problems at [GitHub Issues](https://github.com/thefrederiksen/MyPhotoHelper/issues)
- **Logs**: Application logs are in `%APPDATA%\MyPhotoHelper\Logs\`

## Security Notes

- MyPhotoHelper runs locally on your computer
- No photos are uploaded to external servers
- All processing happens on your machine
- Network access is only used for:
  - Checking for updates (GitHub)
  - Downloading updates when available