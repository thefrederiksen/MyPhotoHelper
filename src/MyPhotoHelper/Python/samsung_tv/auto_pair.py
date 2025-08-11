#!/usr/bin/env python3
"""
Automated pairing script for Samsung TV
This will attempt to pair with your TV - you just need to approve on the TV
"""

import sys
import time
from pathlib import Path
import os

print("Samsung TV Auto-Pairing Script")
print("=" * 40)

# Check dependencies
try:
    from samsungtvws import SamsungTVWS
    print("[OK] samsungtvws library found")
except ImportError:
    print("[ERROR] Missing samsungtvws library")
    print("Install with: pip install git+https://github.com/NickWaterton/samsung-tv-ws-api.git")
    sys.exit(1)

# Configuration
TV_IP = "192.168.1.12"
CLIENT_NAME = "Samsung Frame Uploader"

print(f"\nTV IP: {TV_IP}")
print(f"Client Name: {CLIENT_NAME}")

# Get token path
def get_token_path(ip):
    if os.name == "nt" and os.environ.get("LOCALAPPDATA"):
        base = Path(os.environ["LOCALAPPDATA"])
        token_dir = base / "FrameUploader"
    else:
        token_dir = Path.home() / ".frame_uploader"
    token_dir.mkdir(parents=True, exist_ok=True)
    return token_dir / f"tv_{ip.replace('.', '_')}.token"

token_file = get_token_path(TV_IP)
print(f"Token will be saved to: {token_file}")

# Check if already paired
if token_file.exists():
    print("\n[INFO] Token file already exists. Testing existing pairing...")
    try:
        tv = SamsungTVWS(host=TV_IP, port=8002, token_file=str(token_file), name=CLIENT_NAME)
        tv.open()
        print("[OK] Already paired and working!")
        tv.close()
        sys.exit(0)
    except Exception as e:
        print(f"[WARNING] Existing token invalid: {e}")
        print("Deleting old token and re-pairing...")
        token_file.unlink()

print("\n" + "!" * 60)
print("IMPORTANT: LOOK AT YOUR TV SCREEN NOW!")
print("A popup will appear asking to allow 'Samsung Frame Uploader'")
print("Use your TV remote to select ALLOW")
print("!" * 60)

print("\nAttempting to pair with TV...")
print("This may take up to 30 seconds...\n")

# Try to pair
max_attempts = 3
for attempt in range(1, max_attempts + 1):
    print(f"Pairing attempt {attempt} of {max_attempts}...")
    
    try:
        # Create TV connection
        tv = SamsungTVWS(
            host=TV_IP,
            port=8002,
            token_file=str(token_file),
            name=CLIENT_NAME
        )
        
        # Try to open connection - this triggers pairing
        tv.open()
        print("[OK] Connection opened")
        
        # Try to send a test command
        try:
            tv.send_key("KEY_RETURN")
            print("[SUCCESS] Pairing successful! Token saved.")
            print(f"Token location: {token_file}")
            tv.close()
            break
        except Exception as e:
            error_str = str(e)
            if "ms.remote.touchEnable" in error_str or "event" in error_str:
                print("[SUCCESS] TV responded - pairing successful!")
                print(f"Token saved to: {token_file}")
                break
            else:
                raise
                
    except Exception as e:
        error_str = str(e)
        
        # Check for success indicators
        if "ms.remote.touchEnable" in error_str or "'event'" in error_str:
            print("[SUCCESS] TV responded - pairing successful!")
            print(f"Token saved to: {token_file}")
            break
            
        print(f"[ERROR] {e}")
        
        if "Connection closed" in error_str or "refused" in error_str:
            if attempt < max_attempts:
                print("\nThe TV may be showing a pairing popup.")
                print("Please check your TV and approve the request.")
                print(f"Waiting 10 seconds before retry...\n")
                time.sleep(10)
                # Delete token for fresh attempt
                if token_file.exists():
                    token_file.unlink()
            else:
                print("\n[FAILED] Could not pair after all attempts")
                print("\nTroubleshooting:")
                print("1. Make sure TV is ON (not in standby)")
                print("2. Check TV screen for pairing popup")
                print("3. TV and computer must be on same network")
                print("4. Try restarting your TV")
                sys.exit(1)

# Verify pairing worked
print("\nVerifying pairing...")
if token_file.exists():
    print("[OK] Token file created successfully")
    print("\nYou can now run the automated upload scripts!")
    print("  - python frame_uploader_test.py")
    print("  - python frame_uploader_test.py --continuous")
else:
    print("[ERROR] Token file was not created")
    print("Pairing may have failed")
    sys.exit(1)

print("\nPairing complete!")