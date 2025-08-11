# Samsung Frame TV Upload Scripts

This folder contains scripts for uploading images to Samsung Frame TVs via their Art Mode API.

## TV Configuration
- **Default IP**: 192.168.1.12
- **Model**: Samsung The Frame 65" (QN65LS03BAFXZC)
- **Ports Used**: 8001, 8002, 8080, 9197

## Scripts Overview

### 1. `auto_pair.py` - Automated Pairing
**Status**: ✅ Working  
Establishes initial pairing with the TV. Run this once to create the authentication token.

```bash
python auto_pair.py
```
- Automatically detects existing pairing
- Saves token to `%LOCALAPPDATA%\FrameUploader\tv_192_168_1_12.token`
- Handles pairing popup on TV

### 2. `diagnose_tv.py` - Connection Diagnostics
**Status**: ✅ Working  
Tests all aspects of TV connectivity and Art Mode availability.

```bash
python diagnose_tv.py
```
- Checks network ports
- Verifies pairing token
- Tests WebSocket connection
- Confirms Art Mode support

### 3. `simple_upload.py` - Basic Upload Script
**Status**: ⚠️ Partially Working  
Simplified upload script with timeout protection.

```bash
python simple_upload.py
```
- Resizes image to 1920x1080 for faster upload
- 10-second timeout on upload
- Activates Art Mode after upload

### 4. `frame_uploader_enhanced.py` - Full-Featured Interactive Wizard
**Status**: ⚠️ Needs Testing  
Complete interactive wizard with all features.

```bash
python frame_uploader_enhanced.py
```
Features:
- Network discovery (SSDP)
- Manual IP entry
- Image sizing options
- Matte selection
- Slideshow configuration
- Profile saving

### 5. `frame_uploader_test.py` - Automated Retry Script
**Status**: ⚠️ Needs Fixing  
Automated upload with retry logic.

```bash
python frame_uploader_test.py --continuous
```
- Automatic retries
- Continuous mode
- Progress logging

### 6. Other Scripts
- `frame_uploader.py` - Original basic script
- `frame_uploader_improved.py` - Enhanced detection version
- `quick_test.py` - Quick connection test

## What Works ✅

1. **Network Connectivity** - TV is reachable on all ports
2. **Pairing** - Authentication token creation and validation
3. **WebSocket Connection** - Stable connection to TV
4. **Art Mode Detection** - TV supports and responds to Art Mode commands
5. **Art Mode Activation** - Can successfully enable/disable Art Mode
6. **TV Information Retrieval** - Can fetch model, name, and device info

## What Needs Work 🔧

### 1. Upload Timeout Issue
**Problem**: Image uploads timeout after 10-30 seconds  
**Symptoms**: 
- Upload command hangs indefinitely
- No response from TV after sending image data
- Art Mode activates but image doesn't appear

**Possible Causes**:
- TV takes longer than expected to process images
- Missing response from TV's upload API
- Image format/size requirements not met
- TV needs to be in specific mode before upload

### 2. Unicode Encoding
**Problem**: Windows console encoding issues with special characters  
**Solution Applied**: Replaced emoji with ASCII text in logging

### 3. Interactive Scripts in Background
**Problem**: Scripts requiring user input fail when run in background  
**Solution Needed**: Create fully automated versions

## Installation Requirements

```bash
pip install pillow
pip install websocket-client
pip install git+https://github.com/NickWaterton/samsung-tv-ws-api.git
```

## Quick Start

1. **First Time Setup**:
   ```bash
   # Pair with TV (one time only)
   python auto_pair.py
   ```

2. **Test Connection**:
   ```bash
   # Verify everything is working
   python diagnose_tv.py
   ```

3. **Upload Image**:
   ```bash
   # Try simple upload
   python simple_upload.py
   ```

## Token Location
Pairing tokens are saved to:
- Windows: `%LOCALAPPDATA%\FrameUploader\tv_192_168_1_12.token`
- Linux/Mac: `~/.frame_uploader/tv_192_168_1_12.token`

## Troubleshooting

### TV Not Found
1. Ensure TV is ON (not in standby)
2. Check IP address (Settings > Network > Network Status)
3. Verify both devices on same network
4. Temporarily disable Windows Firewall

### Upload Timeouts
1. Try smaller images (< 1MB)
2. Ensure TV is in Art Mode before uploading
3. Check TV firmware updates
4. Try power cycling the TV

### Pairing Issues
1. Delete existing token file
2. Check TV for pairing popup
3. Ensure TV is in Home mode (not Store mode)
4. Try factory reset of TV's External Device Manager

## Next Steps

1. **Fix Upload Timeout**: 
   - Investigate proper upload API format
   - Test with different image sizes/formats
   - Add better timeout handling
   - Try alternative upload methods

2. **Improve Error Handling**:
   - Better detection of successful uploads
   - Handle partial uploads
   - Add retry logic for timeouts

3. **Add Features**:
   - Batch upload support
   - Image gallery management
   - Scheduled uploads
   - Integration with photo library

## Notes

- The TV model (Samsung The Frame 65" QN65LS03BAFXZC) definitely supports Art Mode
- All network connectivity and authentication is working correctly
- The main issue is the upload API timeout, which may be normal behavior
- Images might be uploading successfully despite timeout messages

## Contact

For issues or improvements, please update this README with findings.