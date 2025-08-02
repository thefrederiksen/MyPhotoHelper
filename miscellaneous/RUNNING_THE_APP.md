# Running MyPhotoHelper

## Quick Start

The easiest way to start MyPhotoHelper is to use the provided batch file:

1. Double-click `start-app.bat` in the root folder
2. The app will build and start automatically
3. Your browser will open to http://localhost:5113

## Manual Start Options

### Option 1: Using Visual Studio
1. Open `src\MyPhotoHelper.sln` in Visual Studio
2. Press F5 or click the "Start" button
3. The app will build and launch

### Option 2: Using Command Line
1. Open a terminal/command prompt
2. Navigate to the project folder: `cd C:\Repos\MyPhotoHelper`
3. Run: `dotnet run --project src\MyPhotoHelper`

### Option 3: Using PowerShell
```powershell
cd C:\Repos\MyPhotoHelper
dotnet run --project src\MyPhotoHelper
```

## What Happens When You Start

1. A startup window appears showing initialization progress
2. The app sets up the database and Python environment
3. Your default browser opens to http://localhost:5113
4. A system tray icon appears for easy access

## Troubleshooting

- **Port Already in Use**: The app uses port 5113. Make sure no other app is using this port.
- **Build Errors**: Run `dotnet build src\MyPhotoHelper.sln` to see detailed error messages
- **Startup Errors**: Check the logs in `%APPDATA%\MyPhotoHelper\Logs\`

## System Requirements

- Windows 10/11
- .NET 9 SDK installed
- About 500MB free disk space for Python environment

## First Time Setup

On first run, the app will:
1. Create necessary directories
2. Initialize the SQLite database
3. Set up the Python virtual environment
4. Install required Python packages

This may take 1-2 minutes on the first run.