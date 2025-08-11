#!/usr/bin/env python3
"""Quick test to upload Fire.jpg to Samsung TV"""

import sys
import os
from pathlib import Path

print("Quick Samsung TV Upload Test")
print("=" * 40)

# Check dependencies
try:
    from samsungtvws import SamsungTVWS
    print("[OK] samsungtvws library found")
except ImportError:
    print("[ERROR] Missing samsungtvws")
    print("Install: pip install git+https://github.com/NickWaterton/samsung-tv-ws-api.git")
    sys.exit(1)

# Configuration
TV_IP = "192.168.1.12"
IMAGE_PATH = r"C:\ReposFred\MyPhotoHelper\src\MyPhotoHelper\Images\Fire.jpg"

print(f"TV IP: {TV_IP}")
print(f"Image: {IMAGE_PATH}")

# Check image exists
if not Path(IMAGE_PATH).exists():
    print(f"[ERROR] Image not found: {IMAGE_PATH}")
    sys.exit(1)
print("[OK] Image file exists")

# Check for token
def get_token_path(ip):
    if os.name == "nt" and os.environ.get("LOCALAPPDATA"):
        base = Path(os.environ["LOCALAPPDATA"])
        token_dir = base / "FrameUploader"
    else:
        token_dir = Path.home() / ".frame_uploader"
    token_dir.mkdir(parents=True, exist_ok=True)
    return token_dir / f"tv_{ip.replace('.', '_')}.token"

token_file = get_token_path(TV_IP)
print(f"Token file: {token_file}")

if not token_file.exists():
    print("[WARNING] No pairing token found!")
    print("You need to pair first using frame_uploader_enhanced.py")
    sys.exit(1)
print("[OK] Token file exists")

# Try to connect and upload
print("\nAttempting connection...")
try:
    tv = SamsungTVWS(
        host=TV_IP,
        port=8002,
        token_file=str(token_file),
        name="Samsung Frame Uploader"
    )
    print("[OK] TV object created")
    
    # Open connection
    print("Opening connection...")
    tv.open()
    print("[OK] Connection opened")
    
    # Get art interface
    art = tv.art()
    print("[OK] Art interface obtained")
    
    # Read image
    print("Reading image file...")
    with open(IMAGE_PATH, 'rb') as f:
        image_data = f.read()
    print(f"[OK] Image loaded ({len(image_data)/1024/1024:.1f} MB)")
    
    # Upload
    print("Uploading to TV...")
    result = art.upload(image_data, file_type="JPEG")
    print("[SUCCESS] Image uploaded!")
    print(f"Result: {result}")
    
    # Activate art mode
    print("Activating Art Mode...")
    art.set_artmode(True)
    print("[OK] Art Mode activated")
    
except Exception as e:
    print(f"[ERROR] {e}")
    
    # Check for common success responses that look like errors
    error_str = str(e)
    if "ms.remote.touchEnable" in error_str or "event" in error_str:
        print("[NOTE] This error often means the command succeeded")
    
    import traceback
    print("\nFull error:")
    traceback.print_exc()

print("\nDone!")