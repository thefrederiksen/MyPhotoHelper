#!/usr/bin/env python3
"""Simple upload script with timeouts"""

import sys
import os
import time
import threading
from pathlib import Path

TV_IP = "192.168.1.12"
IMAGE = r"C:\ReposFred\MyPhotoHelper\src\MyPhotoHelper\Images\Fire.jpg"

print("Simple Samsung Frame Upload")
print("=" * 40)

# Get token path
def get_token_path(ip):
    if os.name == "nt" and os.environ.get("LOCALAPPDATA"):
        base = Path(os.environ["LOCALAPPDATA"])
        token_dir = base / "FrameUploader"
    else:
        token_dir = Path.home() / ".frame_uploader"
    return token_dir / f"tv_{ip.replace('.', '_')}.token"

token_file = get_token_path(TV_IP)

from samsungtvws import SamsungTVWS
from PIL import Image

# Prepare image
print("Preparing image...")
with Image.open(IMAGE) as img:
    # Resize to 1920x1080 for faster upload
    img.thumbnail((1920, 1080), Image.LANCZOS)
    temp_path = Path("temp_upload.jpg")
    img.save(temp_path, "JPEG", quality=85)
    print(f"Image resized to {img.size}")

# Read image data
image_data = temp_path.read_bytes()
print(f"Image size: {len(image_data)/1024:.1f} KB")

# Connect
print(f"Connecting to TV at {TV_IP}...")
tv = SamsungTVWS(host=TV_IP, port=8002, token_file=str(token_file))

# Open with timeout
def open_connection():
    tv.open()

open_thread = threading.Thread(target=open_connection)
open_thread.daemon = True
open_thread.start()
open_thread.join(timeout=5)

if open_thread.is_alive():
    print("[ERROR] Connection timeout")
    sys.exit(1)

print("[OK] Connected")

# Get art interface
art = tv.art()
print("[OK] Art interface ready")

# Upload with timeout
print("Uploading image...")
upload_done = [False]
upload_error = [None]

def do_upload():
    try:
        result = art.upload(image_data, file_type="JPEG")
        upload_done[0] = True
        print(f"[OK] Upload result: {result}")
    except Exception as e:
        upload_error[0] = e

upload_thread = threading.Thread(target=do_upload)
upload_thread.daemon = True
upload_thread.start()
upload_thread.join(timeout=10)

if upload_thread.is_alive():
    print("[ERROR] Upload timeout after 10 seconds")
    print("The upload might still complete in the background")
elif upload_error[0]:
    error_str = str(upload_error[0])
    if "ms.remote.touchEnable" in error_str or "event" in error_str:
        print("[OK] Upload likely succeeded (TV responded)")
    else:
        print(f"[ERROR] Upload failed: {upload_error[0]}")
elif upload_done[0]:
    print("[SUCCESS] Image uploaded!")

# Try to activate art mode
print("Activating Art Mode...")
try:
    art.set_artmode(True)
    print("[OK] Art Mode activated")
except Exception as e:
    if "ms.remote.touchEnable" in str(e) or "event" in str(e):
        print("[OK] Art Mode command sent")
    else:
        print(f"[WARNING] Could not activate: {e}")

# Clean up
temp_path.unlink(missing_ok=True)
print("\nDone! Check your TV's Art Mode")