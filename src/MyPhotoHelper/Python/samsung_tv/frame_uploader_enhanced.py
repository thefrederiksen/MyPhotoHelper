#!/usr/bin/env python3
"""
Samsung Frame Uploader — Enhanced Version with Better Connection Management
"""

from __future__ import annotations

import base64
import json
import os
import socket
import ssl
import sys
import time
import traceback
import threading
from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Dict, List, Optional, Tuple, Any
import re
from datetime import datetime

# --- Optional but recommended dependencies ---
try:
    from PIL import Image
except Exception as e:
    print("Missing dependency: pillow\nInstall with:  pip install pillow", file=sys.stderr)
    raise

try:
    from samsungtvws import SamsungTVWS
except Exception as e:
    print(
        "Missing dependency: samsungtvws\n"
        "Install with:\n"
        "  pip install git+https://github.com/NickWaterton/samsung-tv-ws-api.git\n"
        "  (or) pip install samsungtvws",
        file=sys.stderr,
    )
    raise

try:
    import websocket  # websocket-client
except Exception as e:
    print("Missing dependency: websocket-client\nInstall with:  pip install websocket-client", file=sys.stderr)
    raise


# ------------------------------
# Config & helpers
# ------------------------------

FRAME_RESOLUTION = (3840, 2160)  # Width, Height for 4K Frame TVs
DEBUG = os.environ.get("DEBUG", "").lower() in ("1", "true", "yes")
UPLOAD_TIMEOUT = 60  # seconds
CONNECTION_TIMEOUT = 10  # seconds

MATTE_PRESETS = {
    "0": None,
    "1": "modern_apricot",
    "2": "modern_warm",
    "3": "flexible_apricot",
    "4": "flexible_white",
}

DEFAULT_CLIENT_NAME = "Samsung Frame Uploader"

# Known Samsung TV model patterns
SAMSUNG_MODEL_PATTERNS = [
    r"Samsung",
    r"SAMSUNG",
    r"LS03",  # Frame model
    r"QN\d+",  # QLED models
    r"UN\d+",  # LED models
    r"UE\d+",  # European models
]


class ProgressIndicator:
    """Shows a spinning progress indicator in a separate thread"""
    
    def __init__(self, message: str = "Processing"):
        self.message = message
        self.running = False
        self.thread = None
        self.spinner_chars = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"]
        self.spinner_index = 0
    
    def _spin(self):
        while self.running:
            print(f"\r{self.message} {self.spinner_chars[self.spinner_index]}", end="", flush=True)
            self.spinner_index = (self.spinner_index + 1) % len(self.spinner_chars)
            time.sleep(0.1)
    
    def start(self):
        self.running = True
        self.thread = threading.Thread(target=self._spin)
        self.thread.daemon = True
        self.thread.start()
    
    def stop(self, final_message: str = None):
        self.running = False
        if self.thread:
            self.thread.join(timeout=0.5)
        if final_message:
            print(f"\r{final_message}" + " " * 20)
        else:
            print("\r" + " " * (len(self.message) + 5), end="\r")


def debug_print(msg: str):
    """Print debug messages if DEBUG is enabled"""
    if DEBUG:
        print(f"[DEBUG {datetime.now().strftime('%H:%M:%S')}] {msg}", file=sys.stderr)


def app_data_dir() -> Path:
    """Return a suitable per-user app data folder."""
    if os.name == "nt" and os.environ.get("LOCALAPPDATA"):
        base = Path(os.environ["LOCALAPPDATA"])
        return base / "FrameUploader"
    else:
        return Path.home() / ".frame_uploader"


def settings_path() -> Path:
    """Path to saved settings file"""
    d = app_data_dir()
    d.mkdir(parents=True, exist_ok=True)
    return d / "settings.json"


def load_settings() -> dict:
    """Load saved settings including last used IP"""
    p = settings_path()
    if p.exists():
        try:
            return json.loads(p.read_text(encoding="utf-8"))
        except Exception:
            return {}
    return {}


def save_settings(settings: dict) -> None:
    """Save settings for next run"""
    settings_path().write_text(json.dumps(settings, indent=2), encoding="utf-8")


def token_path_for_ip(ip: str) -> Path:
    d = app_data_dir()
    d.mkdir(parents=True, exist_ok=True)
    return d / f"tv_{ip.replace('.', '_')}.token"


def staging_dir() -> Path:
    d = app_data_dir() / "staging"
    d.mkdir(parents=True, exist_ok=True)
    return d


def profiles_path() -> Path:
    d = app_data_dir()
    d.mkdir(parents=True, exist_ok=True)
    return d / "profiles.json"


def load_profiles() -> Dict[str, dict]:
    p = profiles_path()
    if p.exists():
        try:
            return json.loads(p.read_text(encoding="utf-8"))
        except Exception:
            return {}
    return {}  # name -> dict


def save_profiles(data: Dict[str, dict]) -> None:
    profiles_path().write_text(json.dumps(data, indent=2), encoding="utf-8")


def prompt(text: str, default: Optional[str] = None) -> str:
    if default is not None:
        resp = input(f"{text} [{default}]: ").strip()
        return resp or default
    return input(f"{text}: ").strip()


def prompt_choice(title: str, choices: List[Tuple[str, str]], allow_back=False) -> str:
    """
    choices: list of (key, label)
    returns the selected key
    """
    print(title)
    for k, label in choices:
        print(f"  {k}) {label}")
    if allow_back:
        print("  B) Back")
    while True:
        resp = input("> ").strip()
        if allow_back and resp.upper() == "B":
            return "B"
        if any(resp == k for k, _ in choices):
            return resp
        print("Please choose a valid option.")


def press_enter(msg: str = "Press Enter to continue…"):
    input(msg)


# ------------------------------
# Network Utilities
# ------------------------------

def get_local_ip() -> str:
    """Get the local IP address of this machine"""
    try:
        # Create a socket and connect to an external server to get local IP
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        local_ip = s.getsockname()[0]
        s.close()
        return local_ip
    except Exception:
        return "Unable to determine"


def scan_port(ip: str, port: int, timeout: float = 1.0) -> bool:
    """Check if a port is open on the given IP"""
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(timeout)
        result = sock.connect_ex((ip, port))
        sock.close()
        return result == 0
    except Exception:
        return False


def ping_tv(ip: str) -> bool:
    """Check if TV is reachable via common Samsung TV ports"""
    ports = [8001, 8002, 8080, 9197]  # Common Samsung TV ports
    for port in ports:
        if scan_port(ip, port, timeout=0.5):
            debug_print(f"Found open port {port} on {ip}")
            return True
    return False


def test_tv_connection(ip: str, token_file: Path = None) -> Tuple[bool, str]:
    """
    Test connection to TV and return status with detailed message
    """
    progress = ProgressIndicator("Testing connection")
    progress.start()
    
    try:
        # First check if TV is reachable
        if not ping_tv(ip):
            progress.stop()
            return False, "TV is not reachable (may be off or on different network)"
        
        # Try to get device info
        info = fetch_device_info(ip)
        if not info:
            progress.stop()
            return True, "TV is reachable but API info not available"
        
        # If we have a token, try to validate it
        if token_file and token_file.exists():
            try:
                tv = SamsungTVWS(host=ip, port=8002, token_file=str(token_file))
                tv.open()
                # Try a simple command
                tv.send_key("KEY_RETURN")
                progress.stop()
                return True, "Connection successful with existing token"
            except Exception as e:
                if "ms.remote.touchEnable" in str(e) or "event" in str(e):
                    progress.stop()
                    return True, "Connection successful (TV responded)"
                debug_print(f"Token validation failed: {e}")
                progress.stop()
                return True, "TV reachable but token may need refresh"
        
        progress.stop()
        model = info.get("device", {}).get("modelName", "Unknown")
        return True, f"TV reachable - {model}"
        
    except Exception as e:
        progress.stop()
        return False, f"Connection test failed: {str(e)}"


# ------------------------------
# Enhanced SSDP Discovery
# ------------------------------

def discover_samsung_tvs(timeout: float = 5.0, verbose: bool = False) -> List[str]:
    """
    Enhanced SSDP discovery with better Samsung TV detection
    """
    if verbose:
        print("Starting network scan...")
        local_ip = get_local_ip()
        print(f"Your local IP: {local_ip}")
    
    progress = ProgressIndicator("Scanning network")
    progress.start()
    
    # Multiple SSDP search targets for better coverage
    search_targets = [
        "ssdp:all",
        "urn:samsung.com:device:RemoteControlReceiver:1",
        "urn:schemas-upnp-org:device:MediaRenderer:1",
        "urn:dial-multiscreen-org:service:dial:1"
    ]
    
    found: Dict[str, dict] = {}
    
    for st in search_targets:
        debug_print(f"Searching with ST: {st}")
        msg = "\r\n".join([
            "M-SEARCH * HTTP/1.1",
            "HOST: 239.255.255.250:1900",
            "MAN: \"ssdp:discover\"",
            "MX: 2",
            f"ST: {st}", "", ""]).encode()
        
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM, socket.IPPROTO_UDP)
            s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            s.settimeout(timeout)
            
            # Enable multicast
            try:
                s.setsockopt(socket.IPPROTO_IP, socket.IP_MULTICAST_TTL, 2)
            except Exception as e:
                debug_print(f"Multicast TTL setting failed: {e}")
            
            # Send discovery message
            s.sendto(msg, ("239.255.255.250", 1900))
            
            start = time.time()
            while time.time() - start < timeout:
                try:
                    data, (ip, _) = s.recvfrom(65535)
                    response = data.decode('utf-8', errors='ignore')
                    
                    # More comprehensive Samsung detection
                    is_samsung = False
                    device_info = {"ip": ip, "response": response[:500]}
                    
                    # Check for Samsung indicators
                    for pattern in SAMSUNG_MODEL_PATTERNS:
                        if re.search(pattern, response, re.IGNORECASE):
                            is_samsung = True
                            debug_print(f"Found Samsung device at {ip} (pattern: {pattern})")
                            break
                    
                    # Also check for common TV services
                    if not is_samsung:
                        tv_indicators = [
                            "samsung", "dlna", "upnp", "mediaplayer",
                            "smarttv", "frame", "qled", "dial"
                        ]
                        for indicator in tv_indicators:
                            if indicator in response.lower():
                                is_samsung = True
                                debug_print(f"Found potential Samsung device at {ip} (indicator: {indicator})")
                                break
                    
                    if is_samsung:
                        found[ip] = device_info
                    
                except socket.timeout:
                    break
                except Exception as e:
                    debug_print(f"Error receiving SSDP response: {e}")
                    break
            
            s.close()
        except Exception as e:
            debug_print(f"SSDP discovery error: {e}")
    
    progress.stop()
    
    # Verify discovered IPs are actually Samsung TVs
    verified = []
    if found:
        print(f"Verifying {len(found)} potential device(s)...")
        for ip in found.keys():
            if ping_tv(ip):
                verified.append(ip)
                debug_print(f"Verified Samsung TV at {ip}")
            else:
                debug_print(f"Could not verify {ip} as Samsung TV")
    
    return verified


def scan_subnet_for_tvs(base_ip: str = None, verbose: bool = False) -> List[str]:
    """
    Scan local subnet for Samsung TVs by checking common ports
    """
    if base_ip is None:
        base_ip = get_local_ip()
    
    if base_ip == "Unable to determine":
        return []
    
    # Get subnet base (e.g., 192.168.1)
    parts = base_ip.split('.')
    if len(parts) != 4:
        return []
    
    subnet_base = '.'.join(parts[:3])
    found_tvs = []
    
    if verbose:
        print(f"Scanning subnet {subnet_base}.0/24 for Samsung TVs...")
    
    progress = ProgressIndicator(f"Scanning {subnet_base}.0/24")
    progress.start()
    
    for i in range(1, 255):
        ip = f"{subnet_base}.{i}"
        if ping_tv(ip):
            found_tvs.append(ip)
            if verbose:
                print(f"\r  Found potential TV at {ip}" + " " * 20)
    
    progress.stop()
    return found_tvs


# ------------------------------
# Device info using HTTP API
# ------------------------------

def fetch_device_info(ip: str, timeout: float = 3.0) -> dict:
    """
    Try multiple endpoints to get device metadata
    """
    import urllib.request
    import urllib.error
    
    # Try multiple API endpoints
    endpoints = [
        f"http://{ip}:8001/api/v2/",
        f"http://{ip}:8001/api/v2",
        f"http://{ip}:8080/description.xml",
    ]
    
    for url in endpoints:
        try:
            debug_print(f"Trying endpoint: {url}")
            with urllib.request.urlopen(url, timeout=timeout) as resp:
                raw = resp.read()
                content = raw.decode("utf-8", errors="ignore")
                
                # Try to parse as JSON first
                if url.endswith("/") or url.endswith("/v2"):
                    try:
                        return json.loads(content)
                    except:
                        pass
                
                # Try to extract info from XML
                if "xml" in url:
                    model_match = re.search(r"<modelName>(.*?)</modelName>", content)
                    name_match = re.search(r"<friendlyName>(.*?)</friendlyName>", content)
                    if model_match or name_match:
                        return {
                            "device": {
                                "modelName": model_match.group(1) if model_match else "Unknown",
                                "name": name_match.group(1) if name_match else "Samsung TV"
                            }
                        }
        except urllib.error.HTTPError as e:
            debug_print(f"HTTP error for {url}: {e.code}")
        except Exception as e:
            debug_print(f"Error fetching from {url}: {e}")
    
    return {}


def pretty_device_line(ip: str, info: dict) -> str:
    model = info.get("device", {}).get("modelName") or info.get("device", {}).get("model") or "Unknown"
    name = info.get("device", {}).get("name") or info.get("device", {}).get("id") or "Samsung TV"
    frame = ""
    try:
        # Detect Frame models
        if "Frame" in model or "LS03" in model or "LS03" in name:
            frame = " (The Frame)"
    except Exception:
        pass
    return f"{name}{frame} • {ip} • Model: {model}"


# ------------------------------
# Image processing
# ------------------------------

def _save_jpeg(img: Image.Image, out_path: Path) -> Path:
    out_path.parent.mkdir(parents=True, exist_ok=True)
    img = img.convert("RGB")  # Ensure JPEG-compatible
    img.save(out_path, format="JPEG", quality=92, optimize=True)
    return out_path


def resize_fit(img: Image.Image, target: Tuple[int, int]) -> Image.Image:
    """Fit entire image within target (may letterbox)."""
    tw, th = target
    iw, ih = img.size
    scale = min(tw / iw, th / ih)
    nw, nh = int(iw * scale), int(ih * scale)
    resized = img.resize((nw, nh), Image.LANCZOS)
    canvas = Image.new("RGB", (tw, th), color=(0, 0, 0))
    ox, oy = (tw - nw) // 2, (th - nh) // 2
    canvas.paste(resized, (ox, oy))
    return canvas


def resize_fill_crop(img: Image.Image, target: Tuple[int, int]) -> Image.Image:
    """Fill target area and crop center (no borders)."""
    tw, th = target
    iw, ih = img.size
    scale = max(tw / iw, th / ih)
    nw, nh = int(iw * scale), int(ih * scale)
    resized = img.resize((nw, nh), Image.LANCZOS)
    # Center crop
    left = (nw - tw) // 2
    top = (nh - th) // 2
    right = left + tw
    bottom = top + th
    return resized.crop((left, top, right, bottom))


# ------------------------------
# Direct Art app WebSocket (for slideshow config)
# ------------------------------

def _b64(s: str) -> str:
    return base64.b64encode(s.encode()).decode()


def set_slideshow_over_ws(ip: str, token: str, minutes: str | int, mode: str = "serial",
                          category_id: str = "MY-C0002", client_name: str = DEFAULT_CLIENT_NAME,
                          secure: bool = True, timeout: float = 5.0) -> dict:
    """
    Use direct WebSocket to set auto-rotation (slideshow)
    """
    scheme = "wss" if secure else "ws"
    port = 8002 if secure else 8001
    url = (
        f"{scheme}://{ip}:{port}/api/v2/channels/com.samsung.art-app"
        f"?name={_b64(client_name)}&token={token}"
    )
    sslopt = {"cert_reqs": ssl.CERT_NONE} if secure else None
    ws = websocket.create_connection(url, timeout=timeout, sslopt=sslopt)
    try:
        val = "off" if str(minutes).lower() == "off" else str(int(minutes))
        request_obj = {
            "request": "set_auto_rotation_status",
            "value": val,
            "category_id": category_id,
            "type": "serial" if mode.startswith("serial") else "shuffleslideshow",
            "id": "q3",
        }
        outer = {
            "method": "ms.channel.emit",
            "params": {
                "event": "art_app_request",
                "to": "host",
                "data": json.dumps(request_obj),
            },
        }
        ws.send(json.dumps(outer))
        ws.settimeout(timeout)
        reply = ws.recv()
        try:
            return json.loads(reply)
        except Exception:
            return {"raw": reply}
    finally:
        try:
            ws.close()
        except Exception:
            pass


# ------------------------------
# Network Diagnostics
# ------------------------------

def run_network_diagnostics(ip: str = None):
    """Run network diagnostics to help troubleshoot connection issues"""
    print("\n=== Network Diagnostics ===")
    
    # 1. Show local network info
    local_ip = get_local_ip()
    print(f"Your computer's IP: {local_ip}")
    
    if ip:
        print(f"Target TV IP: {ip}")
        
        # 2. Check if TV is pingable
        print(f"\nChecking connectivity to {ip}...")
        ports_to_check = [
            (8001, "Samsung TV API (HTTP)"),
            (8002, "Samsung TV API (WebSocket/SSL)"),
            (8080, "Alternative API port"),
            (9197, "Samsung SmartThings port"),
        ]
        
        open_ports = []
        for port, description in ports_to_check:
            if scan_port(ip, port, timeout=1.0):
                print(f"  ✓ Port {port} is open ({description})")
                open_ports.append(port)
            else:
                print(f"  ✗ Port {port} is closed or blocked ({description})")
        
        if not open_ports:
            print("\n⚠ No Samsung TV ports are accessible!")
            print("Possible issues:")
            print("  - TV is turned off or in deep sleep mode")
            print("  - TV and computer are on different network segments")
            print("  - Firewall is blocking connections")
            print("  - Wrong IP address")
        else:
            print(f"\n✓ TV appears to be online ({len(open_ports)} ports accessible)")
        
        # 3. Try to get device info
        print(f"\nFetching TV information...")
        info = fetch_device_info(ip)
        if info:
            print(f"  ✓ {pretty_device_line(ip, info)}")
        else:
            print(f"  ✗ Could not fetch TV information (API may be disabled)")
    
    # 4. Check firewall hint
    if os.name == "nt":
        print("\n💡 Windows Firewall Tips:")
        print("  - Windows Defender Firewall may block TV discovery")
        print("  - Try temporarily disabling it for testing")
        print("  - Or add an exception for Python.exe")
    
    print("\n=== End Diagnostics ===\n")


# ------------------------------
# Wizard
# ------------------------------

@dataclass
class WizardConfig:
    tv_ip: str = ""
    client_name: str = DEFAULT_CLIENT_NAME
    token_file: Optional[Path] = None
    image_path: Optional[Path] = None
    sizing_mode: str = "fit"   # fit | fill | asis
    matte: Optional[str] = None
    ensure_artmode_on: bool = True
    slideshow: Optional[str] = None  # None | "off" | "60" | "1440" | "shuffle60"


def step_welcome():
    print("=" * 72)
    print("Samsung Frame Uploader — Enhanced Connection Management")
    print("=" * 72)
    print("Features: Better feedback, saved settings, connection testing\n")
    
    # Load and show saved settings
    settings = load_settings()
    if settings.get("last_ip"):
        print(f"📌 Last used TV: {settings['last_ip']}")
        if settings.get("last_model"):
            print(f"   Model: {settings['last_model']}")
    
    if DEBUG:
        print("🔧 DEBUG MODE ENABLED")
    
    press_enter("Press Enter to begin…")


def step_find_tv(cfg: WizardConfig) -> bool:
    print("\n[1/10] Discover TV")
    
    # Check for saved IP
    settings = load_settings()
    saved_ip = settings.get("last_ip")
    
    choices = [
        ("1", "Quick scan - SSDP discovery (recommended)"),
        ("2", "Deep scan - Check entire subnet (slower but thorough)"),
        ("3", "Enter IP address manually"),
        ("4", "Run network diagnostics"),
    ]
    
    if saved_ip:
        choices.insert(0, ("0", f"Use saved IP: {saved_ip}"))
    
    c = prompt_choice("Choose an option:", choices)
    
    if c == "0" and saved_ip:
        cfg.tv_ip = saved_ip
        print(f"\nUsing saved IP: {saved_ip}")
        # Test connection
        success, message = test_tv_connection(saved_ip)
        print(f"Connection test: {message}")
        if not success:
            print("⚠ Saved IP appears to be offline. Please choose another option.")
            return step_find_tv(cfg)
    elif c == "4":
        ip = prompt("Enter TV IP to diagnose (or press Enter to skip)", "")
        run_network_diagnostics(ip if ip else None)
        return step_find_tv(cfg)
    elif c == "1":
        print("Scanning for Samsung devices (SSDP)…")
        print("This may take up to 5 seconds...")
        ips = discover_samsung_tvs(timeout=5.0, verbose=True)
        if not ips:
            print("\n⚠ No devices found via SSDP.")
            print("This can happen if:")
            print("  - TV is on a different network segment")
            print("  - Windows Firewall is blocking discovery")
            print("  - TV has network discovery disabled")
            print("\nTry option 2 (Deep scan) or 3 (Manual IP)")
            return step_find_tv(cfg)
    elif c == "2":
        print("Performing deep network scan...")
        print("This will check all IPs in your subnet (may take 1-2 minutes)")
        ips = scan_subnet_for_tvs(verbose=True)
        if not ips:
            print("\n⚠ No Samsung TVs found in subnet scan.")
            print("Please enter IP manually or check network connection.")
            c = "3"
    
    if c in ["1", "2"] and ips:
        labeled: List[Tuple[str, str]] = []
        for i, ip in enumerate(sorted(ips), start=1):
            info = fetch_device_info(ip)
            labeled.append((str(i), pretty_device_line(ip, info)))
        print("\nFound TVs:")
        for k, lbl in labeled:
            print(f"  [{k}] {lbl}")
        sel = prompt("Select a device by number or 'R' to rescan", "1")
        if sel.lower() == "r":
            return step_find_tv(cfg)
        try:
            idx = int(sel) - 1
            cfg.tv_ip = sorted(ips)[idx]
        except Exception:
            print("Invalid selection.")
            return step_find_tv(cfg)
    
    if c == "3":
        default_ip = saved_ip or "192.168.1.12"
        ip = prompt(f"Enter TV IP", default_ip)
        cfg.tv_ip = ip.strip()
        
        # Test connection with feedback
        print(f"\nVerifying connection to {cfg.tv_ip}...")
        success, message = test_tv_connection(cfg.tv_ip)
        print(f"Result: {message}")
        
        if not success:
            print("\n⚠ Warning: Cannot connect to TV at this IP!")
            print("The TV might be off or the IP might be wrong.")
            retry = prompt("Continue anyway? (y/N)", "n")
            if not retry.lower().startswith("y"):
                return step_find_tv(cfg)
    
    # Verify device and save IP
    print("\n[2/10] Checking device…")
    info = fetch_device_info(cfg.tv_ip)
    if info:
        model = info.get("device", {}).get("modelName", "Unknown")
        print("• TV Info:", pretty_device_line(cfg.tv_ip, info))
        # Save successful IP and model
        settings = load_settings()
        settings["last_ip"] = cfg.tv_ip
        settings["last_model"] = model
        save_settings(settings)
    else:
        print(f"• TV at {cfg.tv_ip} (Could not fetch device info)")
        # Save IP anyway
        settings = load_settings()
        settings["last_ip"] = cfg.tv_ip
        save_settings(settings)
    
    cont = prompt("Proceed with this device? (Y/n)", "Y")
    return cont.lower().startswith("y")


def step_pair(cfg: WizardConfig) -> bool:
    print("\n[3/10] Pair with TV")
    tok_file = token_path_for_ip(cfg.tv_ip)
    cfg.token_file = tok_file
    print(f"Token file: {tok_file}")
    
    # Check if we already have a valid token
    if tok_file.exists():
        print("Found existing pairing token. Testing...")
        success, message = test_tv_connection(cfg.tv_ip, tok_file)
        if "successful with existing token" in message or "TV responded" in message:
            print(f"✓ {message}")
            print("✓ Already paired! Skipping pairing step.")
            return True
        else:
            print(f"Existing token appears invalid: {message}")
            print("Will attempt re-pairing...")
            tok_file.unlink(missing_ok=True)
    
    print("\n⚠ IMPORTANT:")
    print("  1. Make sure your TV is ON (not in standby)")
    print("  2. The TV will show a popup to approve 'Samsung Frame Uploader'")
    print("  3. Use your TV remote to click 'Allow'\n")
    
    try:
        # First try to connect
        tv = SamsungTVWS(host=cfg.tv_ip, port=8002, token_file=str(tok_file), name=cfg.client_name)
        print("Connecting to TV...")
        
        print("Attempting to pair...")
        print("👀 Look at your TV screen now for the pairing popup!")
        
        max_attempts = 3
        for attempt in range(max_attempts):
            progress = ProgressIndicator(f"Pairing attempt {attempt + 1}/{max_attempts}")
            progress.start()
            
            try:
                # Open connection - this should trigger pairing on first connect
                tv.open()
                time.sleep(1)  # Give it a moment
                
                # If we get here, connection is open - try sending a test command
                # Some TVs need an actual command to trigger the pairing dialog
                try:
                    tv.send_key("KEY_RETURN")
                    progress.stop("✓ Paired successfully!")
                    return True
                except Exception as key_error:
                    # If the error mentions ms.remote.touchEnable, that's actually a success response
                    if "ms.remote.touchEnable" in str(key_error) or "event" in str(key_error):
                        progress.stop("✓ TV responded - connection established!")
                        return True
                    raise key_error
                    
            except Exception as e:
                progress.stop()
                error_str = str(e)
                
                # Check for common success indicators that library might treat as errors
                if "ms.remote.touchEnable" in error_str or "'event'" in error_str:
                    print("✓ TV responded - pairing successful!")
                    return True
                    
                if "Connection closed" in error_str or "Timeout" in error_str or "refused" in error_str:
                    print(f"⚠ Attempt {attempt + 1}/{max_attempts}: Waiting for pairing approval...")
                    
                    if attempt < max_attempts - 1:
                        print("\n📺 Check your TV screen for the pairing popup!")
                        print("   Use your remote to select 'Allow'")
                        print("   (The popup may disappear after 30 seconds)")
                        resp = prompt("\nPress Enter after approving on TV, or 'q' to quit", "")
                        if resp.lower().startswith("q"):
                            return False
                        
                        # Delete token and retry with fresh connection
                        tok_file.unlink(missing_ok=True)
                        tv = SamsungTVWS(host=cfg.tv_ip, port=8002, token_file=str(tok_file), name=cfg.client_name)
                    else:
                        print("\n⚠ Could not pair with TV after multiple attempts.")
                        print("\nTroubleshooting:")
                        print("  1. Make sure TV is powered ON (not in standby)")
                        print("  2. Try going to TV Settings > General > External Device Manager")
                        print("     > Device Connection Manager > Device List")
                        print("     and remove any old 'Samsung Frame Uploader' entries")
                        print("  3. Ensure TV and computer are on the same network")
                        print("  4. Some TVs need to be in 'Home' mode (not 'Store' mode)")
                        print("  5. Try turning TV off and on again")
                        return False
                else:
                    print(f"Unexpected error: {error_str}")
                    if DEBUG:
                        traceback.print_exc()
                    
                    # Try to interpret as success if it seems like a response
                    if attempt == 0 and ("event" in error_str or "ms." in error_str):
                        print("This might actually be a success response. Continuing...")
                        return True
                    
                    if attempt < max_attempts - 1:
                        print("Retrying...")
                        time.sleep(2)
                    else:
                        return False
                        
    except Exception as e:
        print(f"\n⚠ Failed to connect to TV: {e}")
        if DEBUG:
            traceback.print_exc()
        return False
    
    return True


def step_choose_image(cfg: WizardConfig) -> bool:
    print("\n[4/10] Select image to upload")
    
    # Check for recent images in staging dir
    staging = staging_dir()
    recent_files = sorted(staging.glob("*.jpg"), key=lambda p: p.stat().st_mtime, reverse=True)[:5]
    
    if recent_files:
        print("Recent images:")
        for i, f in enumerate(recent_files, 1):
            size = f.stat().st_size / 1024 / 1024  # MB
            print(f"  {i}) {f.name} ({size:.1f} MB)")
        print("  N) Choose new image")
        
        choice = input("> ").strip()
        if choice.isdigit() and 1 <= int(choice) <= len(recent_files):
            cfg.image_path = recent_files[int(choice) - 1]
            with Image.open(cfg.image_path) as im:
                w, h = im.size
                fmt = im.format or "Unknown"
            print(f"Using: {cfg.image_path.name} • {w}x{h} • {fmt}")
            return True
    
    while True:
        p = Path(prompt("Enter path to .jpg or .png"))
        if not p.exists():
            print("File not found. Try again.")
            continue
        try:
            with Image.open(p) as im:
                w, h = im.size
                fmt = im.format or "Unknown"
            print(f"File: {p.name} • {w}x{h} • {fmt}")
            cfg.image_path = p
            return True
        except Exception as e:
            print(f"Not a valid image: {e}")


def step_sizing(cfg: WizardConfig) -> bool:
    print("\n[5/10] Image sizing (The Frame is 3840×2160)")
    c = prompt_choice("Choose:", [
        ("1", "Auto-fit to 3840×2160 (no crop; adds letterbox)"),
        ("2", "Fill & crop center to 3840×2160 (no borders)"),
        ("3", "Leave as-is (TV may matte/letterbox)"),
    ])
    cfg.sizing_mode = {"1": "fit", "2": "fill", "3": "asis"}[c]
    return True


def step_matte(cfg: WizardConfig) -> bool:
    print("\n[6/10] Matte (optional, The Frame only)")
    for k, v in MATTE_PRESETS.items():
        label = "None" if v is None else v.replace("_", " ").title()
        print(f"  {k}) {label}")
    print("  C) Custom matte code")
    sel = input("> ").strip().lower()
    if sel == "c":
        m = input("Enter matte code (e.g., modern_apricot): ").strip()
        cfg.matte = m or None
    else:
        cfg.matte = MATTE_PRESETS.get(sel, None)
    return True


def step_artmode(cfg: WizardConfig) -> bool:
    print("\n[7/10] Ensure Art Mode ON after upload?")
    c = prompt_choice("Select:", [("1", "Yes (recommended)"), ("2", "No")])
    cfg.ensure_artmode_on = (c == "1")
    return True


def _prepare_image(cfg: WizardConfig) -> Tuple[Path, str, Tuple[int, int]]:
    assert cfg.image_path is not None
    src = cfg.image_path
    ext = src.suffix.lower()
    file_type = "JPEG" if ext in (".jpg", ".jpeg") else "PNG" if ext == ".png" else None
    if file_type is None:
        raise ValueError("Use .jpg/.jpeg or .png")

    progress = ProgressIndicator("Preparing image")
    progress.start()
    
    try:
        with Image.open(src) as im:
            orig_size = im.size
            if cfg.sizing_mode == "asis":
                out_path = staging_dir() / src.name
                if file_type == "JPEG":
                    _save_jpeg(im, out_path)
                else:
                    out_path.parent.mkdir(parents=True, exist_ok=True)
                    im.save(out_path, format="PNG", optimize=True)
                progress.stop(f"✓ Image prepared: {orig_size[0]}x{orig_size[1]}")
                return out_path, file_type, orig_size

            if cfg.sizing_mode == "fit":
                processed = resize_fit(im, FRAME_RESOLUTION)
            else:
                processed = resize_fill_crop(im, FRAME_RESOLUTION)

            out_name = f"{src.stem}_3840x2160.jpg" if file_type == "JPEG" else f"{src.stem}_3840x2160.png"
            out_path = staging_dir() / out_name
            if file_type == "JPEG":
                _save_jpeg(processed, out_path)
            else:
                out_path.parent.mkdir(parents=True, exist_ok=True)
                processed.save(out_path, format="PNG", optimize=True)
            
            progress.stop(f"✓ Image resized to {processed.size[0]}x{processed.size[1]}")
            return out_path, file_type, processed.size
    except Exception as e:
        progress.stop(f"✗ Failed to prepare image")
        raise


def upload_with_timeout(tv, art, data: bytes, file_type: str, kwargs: dict, timeout: float = UPLOAD_TIMEOUT) -> Any:
    """Upload with timeout protection"""
    result = [None]
    exception = [None]
    
    def do_upload():
        try:
            result[0] = art.upload(data, file_type=file_type, **kwargs)
        except Exception as e:
            exception[0] = e
    
    thread = threading.Thread(target=do_upload)
    thread.daemon = True
    thread.start()
    thread.join(timeout=timeout)
    
    if thread.is_alive():
        # Upload is taking too long
        raise TimeoutError(f"Upload timed out after {timeout} seconds")
    
    if exception[0]:
        raise exception[0]
    
    return result[0]


def step_upload(cfg: WizardConfig) -> Optional[dict]:
    print("\n[8/10] Uploading image…")
    assert cfg.token_file is not None
    
    # Test connection first
    print("Testing connection...")
    success, message = test_tv_connection(cfg.tv_ip, cfg.token_file)
    if not success:
        print(f"⚠ Connection test failed: {message}")
        retry = prompt("Try anyway? (y/N)", "n")
        if not retry.lower().startswith("y"):
            return None
    else:
        print(f"✓ {message}")
    
    try:
        out_path, file_type, final_size = _prepare_image(cfg)
        file_size = out_path.stat().st_size / 1024 / 1024  # MB
        print(f"• Prepared: {out_path.name} • {final_size[0]}x{final_size[1]} • {file_type} • {file_size:.1f} MB")
    except Exception as e:
        print(f"Failed to prepare image: {e}")
        if DEBUG:
            traceback.print_exc()
        return None

    try:
        print("• Connecting to TV...")
        tv = SamsungTVWS(host=cfg.tv_ip, port=8002, token_file=str(cfg.token_file), name=cfg.client_name)
        
        # Open connection first
        progress = ProgressIndicator("Opening connection")
        progress.start()
        try:
            tv.open()
            progress.stop("✓ Connected")
        except Exception as e:
            progress.stop("✗ Connection failed")
            if "ms.remote.touchEnable" not in str(e) and "event" not in str(e):
                raise
            else:
                print("✓ TV responded (connection established)")
        
        art = tv.art()
        data = Path(out_path).read_bytes()

        kwargs = {}
        if cfg.matte:
            kwargs["matte"] = cfg.matte
            # portrait matte if portrait output
            if final_size[1] > final_size[0]:
                kwargs["portrait_matte"] = cfg.matte

        print(f"• Uploading {file_size:.1f} MB to TV (timeout: {UPLOAD_TIMEOUT}s)...")
        progress = ProgressIndicator("Uploading")
        progress.start()
        
        start_time = time.time()
        try:
            resp = upload_with_timeout(tv, art, data, file_type, kwargs, timeout=UPLOAD_TIMEOUT)
            elapsed = time.time() - start_time
            progress.stop(f"✓ Upload successful! ({elapsed:.1f}s)")
        except TimeoutError as e:
            progress.stop(f"✗ Upload timed out after {UPLOAD_TIMEOUT}s")
            print("\n⚠ Upload is taking too long. Possible issues:")
            print("  - TV might be processing the image (wait and check Art Mode)")
            print("  - Network connection might be slow")
            print("  - TV might not support Art Mode uploads")
            return None
        except Exception as e:
            progress.stop(f"✗ Upload failed")
            raise
        
        if cfg.ensure_artmode_on:
            print("• Setting Art Mode ON...")
            try:
                art.set_artmode(True)
                print("  ✓ Art Mode activated")
            except Exception as e:
                if "ms.remote.touchEnable" in str(e) or "event" in str(e):
                    print("  ✓ Art Mode command sent")
                else:
                    print(f"  ⚠ Could not toggle Art Mode: {e}")
        
        if DEBUG:
            print(f"TV response: {resp}")
        
        # Save successful upload info
        settings = load_settings()
        settings["last_upload"] = {
            "file": out_path.name,
            "size": f"{final_size[0]}x{final_size[1]}",
            "timestamp": datetime.now().isoformat()
        }
        save_settings(settings)
        
        return resp
        
    except Exception as e:
        print(f"\n⚠ Upload failed: {e}")
        if DEBUG:
            traceback.print_exc()
        print("\nTroubleshooting:")
        print("  - Make sure TV is ON (not standby)")
        print("  - Check if Art Mode is available on your TV")
        print("  - Try re-pairing (delete token file and restart)")
        print("  - Ensure TV and computer are on same network")
        print("  - Try a smaller image first")
        return None


def step_set_current(cfg: WizardConfig, upload_resp: Optional[dict]) -> bool:
    print("\n[9/10] Show the uploaded image now?")
    print("Note: Many Frame models show the latest uploaded image automatically.")
    c = prompt_choice("Select:", [("1", "Yes"), ("2", "No")])
    if c == "2" or not upload_resp:
        return True

    progress = ProgressIndicator("Activating image")
    progress.start()
    
    try:
        tv = SamsungTVWS(host=cfg.tv_ip, port=8002, token_file=str(cfg.token_file), name=cfg.client_name)
        tv.art().set_artmode(True)
        progress.stop("✓ Art Mode activated (should show latest upload)")
    except Exception as e:
        progress.stop("⚠ Could not activate")
        print(f"  Select image manually from Art Mode if needed: {e}")
    return True


def step_slideshow(cfg: WizardConfig) -> bool:
    print("\n[10/10] Configure slideshow (optional)")
    c = prompt_choice("Choose:", [
        ("1", "Off"),
        ("2", "Every 60 minutes (serial)"),
        ("3", "Every 1440 minutes (daily)"),
        ("4", "Shuffle every 60 minutes"),
        ("5", "Skip slideshow config"),
    ])
    
    if c == "5":
        return True
    
    cfg.slideshow = {"1": "off", "2": "60", "3": "1440", "4": "shuffle60"}[c]

    if cfg.slideshow and cfg.slideshow != "off":
        token = ""
        try:
            token = Path(cfg.token_file).read_text().strip() if cfg.token_file else ""
        except Exception:
            pass

        if not token:
            print("• Warning: Could not read token for slideshow config.")
            return True

        minutes = "60" if cfg.slideshow.endswith("60") else "1440"
        mode = "serial" if cfg.slideshow in ("60", "1440") else "shuffle"
        
        progress = ProgressIndicator("Configuring slideshow")
        progress.start()
        
        try:
            resp = set_slideshow_over_ws(cfg.tv_ip, token, minutes=minutes, mode=mode)
            progress.stop(f"✓ Slideshow configured: {cfg.slideshow}")
            if DEBUG:
                print(f"Response: {resp}")
        except Exception as e:
            progress.stop("✗ Could not configure slideshow")
            print(f"  Error: {e}")
            if DEBUG:
                traceback.print_exc()
    return True


def step_save_profile(cfg: WizardConfig) -> None:
    print("\nSave profile for quick access?")
    c = prompt_choice("Save profile?", [("1", "Yes"), ("2", "No")])
    if c == "2":
        print("\n✓ Done! Your photo should now be on The Frame.")
        print(f"📺 TV IP {cfg.tv_ip} has been saved for next time.")
        return

    name = prompt("Profile name", "LivingRoomFrame")
    profs = load_profiles()
    profs[name] = {
        "tv_ip": cfg.tv_ip,
        "client_name": cfg.client_name,
        "token_file": str(cfg.token_file) if cfg.token_file else None,
        "sizing_mode": cfg.sizing_mode,
        "matte": cfg.matte,
        "ensure_artmode_on": cfg.ensure_artmode_on,
        "slideshow": cfg.slideshow,
    }
    save_profiles(profs)
    print(f"✓ Saved profile '{name}'.")
    print("\n✓ Done! Your photo should now be on The Frame.")
    print(f"📺 TV IP {cfg.tv_ip} has been saved for next time.")


def main():
    global DEBUG
    # Check for debug mode
    if "--debug" in sys.argv or DEBUG:
        os.environ["DEBUG"] = "1"
        DEBUG = True
    
    step_welcome()
    cfg = WizardConfig()

    if not step_find_tv(cfg):
        print("Canceled.")
        return

    if not step_pair(cfg):
        print("Pairing canceled.")
        return

    if not step_choose_image(cfg):
        print("No image chosen; exiting.")
        return

    if not step_sizing(cfg):
        print("Sizing step canceled.")
        return

    if not step_matte(cfg):
        print("Matte step canceled.")
        return

    if not step_artmode(cfg):
        print("Art Mode step canceled.")
        return

    upload_resp = step_upload(cfg)
    if upload_resp:
        step_set_current(cfg, upload_resp)
        step_slideshow(cfg)
    
    step_save_profile(cfg)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\n⚠ Aborted by user (Ctrl+C)")
        print("Your progress has been saved.")
    except Exception as e:
        print(f"\n⚠ Unexpected error: {e}")
        if DEBUG:
            traceback.print_exc()
        print("\nRun with --debug flag for more details")