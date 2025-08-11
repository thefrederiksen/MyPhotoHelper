#!/usr/bin/env python3
"""Diagnose Samsung TV connection and Art Mode"""

import sys
import socket
import time
from pathlib import Path
import os
import json

print("Samsung TV Diagnostic Tool")
print("=" * 40)

TV_IP = "192.168.1.12"

# 1. Test network connectivity
print(f"\n1. Testing network connectivity to {TV_IP}...")
ports = [8001, 8002, 8080, 9197]
open_ports = []
for port in ports:
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(1)
        result = sock.connect_ex((TV_IP, port))
        sock.close()
        if result == 0:
            print(f"   [OK] Port {port} is open")
            open_ports.append(port)
        else:
            print(f"   [--] Port {port} is closed")
    except Exception as e:
        print(f"   [ERROR] Port {port}: {e}")

if not open_ports:
    print("\n[ERROR] No ports are open. TV may be off or unreachable")
    sys.exit(1)

# 2. Check token
print("\n2. Checking pairing token...")
def get_token_path(ip):
    if os.name == "nt" and os.environ.get("LOCALAPPDATA"):
        base = Path(os.environ["LOCALAPPDATA"])
        token_dir = base / "FrameUploader"
    else:
        token_dir = Path.home() / ".frame_uploader"
    return token_dir / f"tv_{ip.replace('.', '_')}.token"

token_file = get_token_path(TV_IP)
if token_file.exists():
    print(f"   [OK] Token exists: {token_file}")
    token_content = token_file.read_text().strip()
    print(f"   Token length: {len(token_content)} chars")
else:
    print(f"   [ERROR] No token at: {token_file}")
    sys.exit(1)

# 3. Try to get TV info via HTTP
print("\n3. Getting TV information...")
import urllib.request
import urllib.error

try:
    url = f"http://{TV_IP}:8001/api/v2/"
    with urllib.request.urlopen(url, timeout=3) as resp:
        data = json.loads(resp.read().decode())
        if "device" in data:
            print(f"   TV Name: {data['device'].get('name', 'Unknown')}")
            print(f"   Model: {data['device'].get('modelName', 'Unknown')}")
            print(f"   ID: {data['device'].get('id', 'Unknown')}")
except Exception as e:
    print(f"   [WARNING] Could not get TV info: {e}")

# 4. Test WebSocket connection
print("\n4. Testing WebSocket connection...")
try:
    from samsungtvws import SamsungTVWS
    print("   [OK] samsungtvws library loaded")
except ImportError:
    print("   [ERROR] samsungtvws not installed")
    sys.exit(1)

try:
    print(f"   Creating connection to {TV_IP}...")
    tv = SamsungTVWS(
        host=TV_IP,
        port=8002,
        token_file=str(token_file),
        name="Samsung Frame Uploader"
    )
    print("   [OK] TV object created")
    
    print("   Opening connection (timeout 10s)...")
    # Add timeout wrapper
    import threading
    connected = [False]
    error = [None]
    
    def try_connect():
        try:
            tv.open()
            connected[0] = True
        except Exception as e:
            error[0] = e
    
    thread = threading.Thread(target=try_connect)
    thread.daemon = True
    thread.start()
    thread.join(timeout=10)
    
    if thread.is_alive():
        print("   [ERROR] Connection timed out after 10 seconds")
        print("   TV may be in standby or not responding")
    elif connected[0]:
        print("   [OK] Connection opened successfully!")
        
        # 5. Test Art Mode
        print("\n5. Testing Art Mode capabilities...")
        try:
            art = tv.art()
            print("   [OK] Art interface obtained")
            
            # Try to get art mode status
            print("   Testing art mode commands...")
            try:
                # This might fail but tells us if art mode is available
                art.get_artmode()
                print("   [OK] Art mode appears to be available")
            except Exception as e:
                if "ms.remote.touchEnable" in str(e) or "event" in str(e):
                    print("   [OK] TV responded (art mode likely available)")
                else:
                    print(f"   [WARNING] Art mode status check failed: {e}")
            
        except Exception as e:
            print(f"   [ERROR] Could not access art mode: {e}")
            print("   Your TV model may not support Art Mode uploads")
        
        # Close connection
        try:
            tv.close()
            print("   [OK] Connection closed")
        except:
            pass
            
    else:
        print(f"   [ERROR] Connection failed: {error[0]}")
        if error[0]:
            error_str = str(error[0])
            if "ms.remote.touchEnable" in error_str or "event" in error_str:
                print("   [NOTE] This error often means the TV is responding")
                
except Exception as e:
    print(f"   [ERROR] Unexpected error: {e}")

print("\n" + "=" * 40)
print("Diagnostic Summary:")
print("=" * 40)

if open_ports:
    print(f"[OK] TV is reachable on {len(open_ports)} port(s)")
else:
    print("[ERROR] TV is not reachable")
    
if token_file.exists():
    print("[OK] Pairing token exists")
else:
    print("[ERROR] No pairing token")

print("\nRecommendations:")
if not open_ports:
    print("- Make sure TV is ON (not in standby)")
    print("- Check TV IP address is correct")
elif not token_file.exists():
    print("- Run pairing script: python auto_pair.py")
else:
    print("- Try turning TV off and on again")
    print("- Make sure Art Mode is available on your TV model")
    print("- Check if TV firmware needs updating")

print("\nDone!")