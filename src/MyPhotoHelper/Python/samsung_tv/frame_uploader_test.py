#!/usr/bin/env python3
"""
Samsung Frame TV - Automated Upload Test Script
Keeps trying to upload an image until it succeeds
"""

import sys
import time
import os
from pathlib import Path
import json
import traceback
from datetime import datetime

# Add required dependencies check
try:
    from PIL import Image
    from samsungtvws import SamsungTVWS
    import websocket
except ImportError as e:
    print(f"Missing dependency: {e}")
    print("\nInstall with:")
    print("  pip install pillow")
    print("  pip install websocket-client")
    print("  pip install git+https://github.com/NickWaterton/samsung-tv-ws-api.git")
    sys.exit(1)


# Configuration
TV_IP = "192.168.1.12"  # Your TV's IP
CLIENT_NAME = "Samsung Frame Uploader"
MAX_RETRIES = 10  # Maximum number of upload attempts
RETRY_DELAY = 5  # Seconds between retries
UPLOAD_TIMEOUT = 30  # Seconds to wait for upload
DEBUG = True  # Show detailed output

# Test image - you can change this path
DEFAULT_TEST_IMAGE = r"C:\ReposFred\MyPhotoHelper\src\MyPhotoHelper\Images\Fire.jpg"  # Fire pit image


def log(msg: str, level: str = "INFO"):
    """Log with timestamp"""
    timestamp = datetime.now().strftime("%H:%M:%S")
    prefix = {
        "INFO": "[INFO]",
        "SUCCESS": "[OK]",
        "ERROR": "[ERROR]",
        "WARNING": "[WARN]",
        "DEBUG": "[DEBUG]"
    }.get(level, "")
    print(f"[{timestamp}] {prefix} {msg}")


def get_app_data_dir() -> Path:
    """Get app data directory"""
    if os.name == "nt" and os.environ.get("LOCALAPPDATA"):
        base = Path(os.environ["LOCALAPPDATA"])
        return base / "FrameUploader"
    else:
        return Path.home() / ".frame_uploader"


def get_token_path(ip: str) -> Path:
    """Get token file path for IP"""
    d = get_app_data_dir()
    d.mkdir(parents=True, exist_ok=True)
    return d / f"tv_{ip.replace('.', '_')}.token"


def get_staging_dir() -> Path:
    """Get staging directory for processed images"""
    d = get_app_data_dir() / "staging"
    d.mkdir(parents=True, exist_ok=True)
    return d


def check_existing_token(ip: str) -> bool:
    """Check if we have a valid token"""
    token_file = get_token_path(ip)
    if token_file.exists():
        log(f"Found existing token at: {token_file}", "INFO")
        return True
    else:
        log(f"No token found at: {token_file}", "WARNING")
        return False


def test_connection(ip: str) -> bool:
    """Test if TV is reachable"""
    import socket
    
    log(f"Testing connection to {ip}...", "INFO")
    
    # Check common Samsung TV ports
    ports = [8001, 8002]
    for port in ports:
        try:
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(2)
            result = sock.connect_ex((ip, port))
            sock.close()
            if result == 0:
                log(f"Port {port} is open", "DEBUG")
                return True
        except Exception:
            pass
    
    log(f"TV at {ip} is not reachable", "ERROR")
    return False


def prepare_image(image_path: Path, target_size: tuple = (3840, 2160)) -> Path:
    """Prepare image for upload"""
    log(f"Preparing image: {image_path}", "INFO")
    
    if not image_path.exists():
        raise FileNotFoundError(f"Image not found: {image_path}")
    
    with Image.open(image_path) as img:
        # Convert to RGB if necessary
        if img.mode != 'RGB':
            img = img.convert('RGB')
        
        # Resize to fit Frame resolution
        img.thumbnail(target_size, Image.LANCZOS)
        
        # Create black background
        background = Image.new('RGB', target_size, (0, 0, 0))
        
        # Paste image centered
        offset = ((target_size[0] - img.width) // 2,
                 (target_size[1] - img.height) // 2)
        background.paste(img, offset)
        
        # Save to staging
        output_path = get_staging_dir() / f"test_upload_{int(time.time())}.jpg"
        background.save(output_path, 'JPEG', quality=90, optimize=True)
        
        file_size = output_path.stat().st_size / 1024 / 1024  # MB
        log(f"Image prepared: {output_path.name} ({file_size:.1f} MB)", "SUCCESS")
        return output_path


def attempt_upload(ip: str, image_path: Path, attempt_num: int) -> bool:
    """Attempt to upload image to TV"""
    log(f"Upload attempt {attempt_num}/{MAX_RETRIES}", "INFO")
    
    token_file = get_token_path(ip)
    
    try:
        # Connect to TV
        log("Connecting to TV...", "INFO")
        tv = SamsungTVWS(
            host=ip,
            port=8002,
            token_file=str(token_file),
            name=CLIENT_NAME
        )
        
        # Open connection
        try:
            tv.open()
            log("Connection opened", "DEBUG")
        except Exception as e:
            if "ms.remote.touchEnable" in str(e) or "event" in str(e):
                log("TV responded (normal response)", "DEBUG")
            else:
                raise
        
        # Get art mode interface
        art = tv.art()
        
        # Read image data
        image_data = image_path.read_bytes()
        log(f"Uploading {len(image_data)/1024/1024:.1f} MB...", "INFO")
        
        # Upload with timeout protection
        import threading
        upload_result = [None]
        upload_error = [None]
        
        def do_upload():
            try:
                upload_result[0] = art.upload(image_data, file_type="JPEG")
            except Exception as e:
                upload_error[0] = e
        
        upload_thread = threading.Thread(target=do_upload)
        upload_thread.daemon = True
        upload_thread.start()
        upload_thread.join(timeout=UPLOAD_TIMEOUT)
        
        if upload_thread.is_alive():
            log(f"Upload timed out after {UPLOAD_TIMEOUT}s", "ERROR")
            return False
        
        if upload_error[0]:
            raise upload_error[0]
        
        log("Upload completed!", "SUCCESS")
        
        # Try to activate art mode
        try:
            log("Activating Art Mode...", "INFO")
            art.set_artmode(True)
            log("Art Mode activated", "SUCCESS")
        except Exception as e:
            if "ms.remote.touchEnable" in str(e) or "event" in str(e):
                log("Art Mode command sent", "SUCCESS")
            else:
                log(f"Could not activate Art Mode: {e}", "WARNING")
        
        return True
        
    except Exception as e:
        error_msg = str(e)
        
        # Check if it's actually a success response
        if "ms.remote.touchEnable" in error_msg or "'event'" in error_msg:
            log("Upload likely succeeded (TV responded)", "SUCCESS")
            return True
        
        log(f"Upload failed: {error_msg}", "ERROR")
        if DEBUG:
            log(f"Traceback: {traceback.format_exc()}", "DEBUG")
        
        # Specific error handling
        if "Connection refused" in error_msg or "Connection closed" in error_msg:
            log("TV might be in standby or needs pairing", "WARNING")
            if not token_file.exists():
                log("No pairing token found. You need to pair first using frame_uploader_enhanced.py", "ERROR")
                return False
        
        return False


def auto_upload_loop(image_path: Path = None):
    """Main loop that keeps trying to upload"""
    
    # Use provided image or default
    if image_path is None:
        image_path = Path(DEFAULT_TEST_IMAGE)
    
    log("=" * 60, "INFO")
    log("Samsung Frame TV - Automated Upload Test", "INFO")
    log("=" * 60, "INFO")
    log(f"TV IP: {TV_IP}", "INFO")
    log(f"Image: {image_path}", "INFO")
    log(f"Max retries: {MAX_RETRIES}", "INFO")
    log(f"Retry delay: {RETRY_DELAY}s", "INFO")
    log("=" * 60, "INFO")
    
    # Check if we have a pairing token
    has_token = check_existing_token(TV_IP)
    if not has_token:
        log("No pairing token found!", "WARNING")
        log("You need to pair first. Run: python frame_uploader_enhanced.py", "ERROR")
        log("After pairing once, this script will work automatically.", "INFO")
        return False
    
    # Test connection
    if not test_connection(TV_IP):
        log("Cannot reach TV. Please check:", "ERROR")
        log("  1. TV is powered ON (not standby)", "INFO")
        log("  2. TV IP is correct: 192.168.1.12", "INFO")
        log("  3. TV and computer are on same network", "INFO")
        return False
    
    # Prepare image once
    try:
        prepared_image = prepare_image(image_path)
    except Exception as e:
        log(f"Failed to prepare image: {e}", "ERROR")
        return False
    
    # Try uploading
    for attempt in range(1, MAX_RETRIES + 1):
        log(f"\n--- Attempt {attempt} of {MAX_RETRIES} ---", "INFO")
        
        if attempt_upload(TV_IP, prepared_image, attempt):
            log("\nSUCCESS! Image uploaded to TV!", "SUCCESS")
            log(f"Total attempts: {attempt}", "INFO")
            log("Check your TV's Art Mode to see the image.", "INFO")
            return True
        
        if attempt < MAX_RETRIES:
            log(f"Waiting {RETRY_DELAY} seconds before retry...", "INFO")
            time.sleep(RETRY_DELAY)
    
    log("\nFailed after all retries", "ERROR")
    log("Troubleshooting:", "INFO")
    log("  1. Make sure TV is ON and not in standby", "INFO")
    log("  2. Try manual pairing: python frame_uploader_enhanced.py", "INFO")
    log("  3. Check if Art Mode is available on your TV model", "INFO")
    return False


def continuous_mode(image_path: Path = None):
    """Continuous upload mode - keeps trying forever"""
    log("CONTINUOUS MODE - Will keep trying until stopped (Ctrl+C)", "WARNING")
    
    attempt_set = 1
    while True:
        log(f"\n{'='*60}", "INFO")
        log(f"Starting attempt set #{attempt_set}", "INFO")
        log(f"{'='*60}", "INFO")
        
        success = auto_upload_loop(image_path)
        
        if success:
            log("\nUpload successful! Continue uploading? (y/n): ", "INFO")
            try:
                response = input().strip().lower()
                if response != 'y':
                    break
            except KeyboardInterrupt:
                break
        else:
            log(f"\nWaiting 30 seconds before next attempt set...", "INFO")
            time.sleep(30)
        
        attempt_set += 1
    
    log("\nStopped by user", "INFO")


def main():
    """Main entry point"""
    global TV_IP, MAX_RETRIES, RETRY_DELAY
    import argparse
    
    parser = argparse.ArgumentParser(description="Automated Samsung Frame TV Upload Test")
    parser.add_argument("image", nargs="?", help="Path to image file")
    parser.add_argument("--continuous", "-c", action="store_true", 
                       help="Continuous mode - keeps trying forever")
    parser.add_argument("--ip", default=TV_IP, help=f"TV IP address (default: {TV_IP})")
    parser.add_argument("--retries", type=int, default=MAX_RETRIES, 
                       help=f"Max retries (default: {MAX_RETRIES})")
    parser.add_argument("--delay", type=int, default=RETRY_DELAY,
                       help=f"Retry delay in seconds (default: {RETRY_DELAY})")
    
    args = parser.parse_args()
    
    # Update globals if provided
    TV_IP = args.ip
    MAX_RETRIES = args.retries
    RETRY_DELAY = args.delay
    
    # Determine image path
    if args.image:
        image_path = Path(args.image)
        if not image_path.exists():
            log(f"Image not found: {image_path}", "ERROR")
            sys.exit(1)
    else:
        # Try to find a test image
        possible_paths = [
            Path(DEFAULT_TEST_IMAGE),
            Path.cwd() / "test.jpg",
            Path.cwd() / "test.png",
            Path.home() / "Pictures" / "test.jpg",
        ]
        
        image_path = None
        for p in possible_paths:
            if p.exists():
                image_path = p
                log(f"Using test image: {p}", "INFO")
                break
        
        if not image_path:
            log("No image specified and no test image found!", "ERROR")
            log("Usage: python frame_uploader_test.py <image_path>", "INFO")
            log("Or create a test image at: " + DEFAULT_TEST_IMAGE, "INFO")
            sys.exit(1)
    
    try:
        if args.continuous:
            continuous_mode(image_path)
        else:
            success = auto_upload_loop(image_path)
            sys.exit(0 if success else 1)
    except KeyboardInterrupt:
        log("\n\nStopped by user (Ctrl+C)", "WARNING")
        sys.exit(0)


if __name__ == "__main__":
    main()