# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Communication Guidelines

- Use simple, user-friendly language in all UI text
- Avoid technical jargon or "nerd speak" when communicating with users
- Keep messages concise and clear
- Don't state the obvious (e.g., "This page refreshes automatically" is unnecessary)

## Project Overview

MyPhotoHelper (also referenced as FaceVault in documentation) is a .NET 9 Blazor Server application for photo organization and management with AI-powered features. The application combines C# for the web UI and database operations with Python for image processing and AI analysis.

## Key Commands

### Build and Run
- `dotnet build src/MyPhotoHelper.sln` - Build the solution
- `dotnet run --project src/MyPhotoHelper` - Run the application
- Application runs on `http://localhost:5113` by default

### Testing
- `dotnet test src/MyPhotoHelper.Tests` - Run C# unit tests
- `src/MyPhotoHelper/Python/run_tests.bat` - Run Python tests (Windows)
- `python -m pytest src/MyPhotoHelper/Python/test/` - Run Python tests directly

### Database
- Database file: `src/MyPhotoHelper/Database/dev_myphotohelper.db` (SQLite)
- Migrations in: `src/MyPhotoHelper/Database/`
- EF Core commands should be run from `src/MyPhotoHelper/` directory

### Python Environment
- Python requirements: `src/MyPhotoHelper/Python/requirements.txt`
- Main dependencies: Pillow, numpy, pillow-heif, requests
- Python modules are in `src/MyPhotoHelper/Python/`

## Architecture

### Technology Stack
- **Frontend**: Blazor Server (C#/.NET 9)
- **Backend**: ASP.NET Core with Entity Framework Core
- **Database**: SQLite with code-first migrations
- **Python Integration**: CSnakes Runtime for Python interop
- **UI Framework**: Bootstrap with custom CSS

### Project Structure
```
src/
├── MyPhotoHelper/                 # Main Blazor Server application
│   ├── Components/               # Blazor components
│   ├── Data/                    # EF Core DbContext
│   ├── Database/                # SQL scripts and schema docs
│   ├── Models/                  # Entity models (tbl_* prefix)
│   ├── Pages/                   # Blazor pages
│   ├── Python/                  # Python modules for image processing
│   ├── Services/                # Business logic services
│   └── docs/                    # Implementation and design docs
└── MyPhotoHelper.Tests/         # Unit tests
```

### Database Design
- Uses `tbl_` prefix for all table names (e.g., `tbl_images`, `tbl_image_metadata`)
- Modular design separating file system info, metadata, and AI analysis
- Entity models mirror table structure with same naming
- Main tables: `tbl_images`, `tbl_image_metadata`, `tbl_image_analysis`, `tbl_app_settings`

### Python Integration
- Python code in `Python/` directory is copied to output during build
- CSnakes Runtime provides C#-Python interoperability
- Main Python modules:
  - `image_analysis_module.py` - AI image analysis
  - `heic_converter.py` - HEIC format conversion
  - `database_manager.py` - Database operations

## Key Services and Components

### Core Services
- `DatabaseInitializationService` - Database setup and migrations
- `BackgroundTaskService` - Async background processing
- `SystemTrayService` - System tray integration
- `Logger` - Application logging

### Application Startup
- WinForms entry point in `Program.cs` with single-instance checking
- `BlazorServerStarter` manages Blazor server startup
- Can run as WinForms app (default) or console app based on build configuration

### Important Configuration
- `UseWinFormsStartup` property controls startup mode
- `WINFORMS_APP` conditional compilation symbol
- Default port: 5113
- SQLite connection string in `MyPhotoHelperDbContext.cs`

## Development Notes

### File Organization
- Backup files are in `Backup_RemovedFiles/` and excluded from build
- Test images are in `MyPhotoHelper.Tests/Images/`
- Documentation is primarily in `docs/` directory

### Python Development
- Use `pytest.ini` for test configuration
- Python tests should go in `Python/test/` directory
- Requirements are managed in `Python/requirements.txt`

### Database Development
- Use Entity Framework migrations for schema changes
- Database schema is documented in `Database/SCHEMA.md`
- Entity models use the same primary key pattern (shared PK design)

## Testing Strategy
- C# unit tests use Microsoft Test Framework
- Python tests use pytest
- Integration testing covers C#-Python interop
- Manual testing documented in implementation docs

## UI/UX Guidelines

### Responsiveness
- **CRITICAL**: Every user action must provide immediate visual feedback
- When navigating between pages, show a loading spinner immediately
- Button clicks should disable the button and show a spinner while processing
- Use the `LoadingSpinner` component for consistent loading states
- The `PageTransition` component provides automatic navigation feedback

### Communication Style
- Use simple, non-technical language when communicating with users
- Avoid "nerd speak" and technical jargon
- Examples:
  - ❌ "This page refreshes automatically"
  - ✅ "Your memories will appear here as photos are discovered"
  - ❌ "Processing image metadata extraction"
  - ✅ "Reading photo information"

### Error Handling
- All pages must be wrapped in `AppErrorBoundary` component
- Errors should show user-friendly messages with:
  - Clear explanation of what went wrong
  - A "Copy Error" button for technical details
  - Options to recover (Try Again, Go Home)
- Never show raw exception messages to users

### Performance
- Pages should load incrementally - show what's available immediately
- Use virtualization for large lists
- Implement proper loading states for all async operations
- Background tasks should not block the UI