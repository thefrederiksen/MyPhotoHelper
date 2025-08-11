#!/usr/bin/env python3
"""
Samsung Frame Uploader — Interactive Wizard (Windows 11-friendly)

What this script does:
1) Finds your Samsung TV (optional network scan via SSDP) or lets you enter the IP.
2) Pairs with the TV (one-time) and saves the token.
3) Lets you pick an image and optionally resize it to 3840x2160 (fit or fill+crop).
4) Uploads to The Frame's Art Mode (via samsungtvws).
5) Optionally ensures Art Mode is ON and configures a simple slideshow.

Dependencies (install once):
    pip install pillow websocket-client
    pip install git+https://github.com/NickWaterton/samsung-tv-ws-api.git
      (or) pip install samsungtvws

Run:
    python frame_uploader.py
"""

from __future__ import annotations

import base64
import json
import os
import socket
import ssl
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Optional, Tuple

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

MATTE_PRESETS = {
    "0": None,
    "1": "modern_apricot",
    "2": "modern_warm",
    "3": "flexible_apricot",
    "4": "flexible_white",
}

DEFAULT_CLIENT_NAME = "Samsung Frame Uploader"


def app_data_dir() -> Path:
    """Return a suitable per-user app data folder."""
    if os.name == "nt" and os.environ.get("LOCALAPPDATA"):
        base = Path(os.environ["LOCALAPPDATA"])
        return base / "FrameUploader"
    else:
        return Path.home() / ".frame_uploader"


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
# SSDP Discovery
# ------------------------------

def discover_samsung_tvs(timeout: float = 3.0) -> List[str]:
    """
    Broadcast SSDP M-SEARCH and return a list of responding IPs that appear to be Samsung devices.
    """
    msg = "\r\n".join([
        "M-SEARCH * HTTP/1.1",
        "HOST: 239.255.255.250:1900",
        "MAN: \"ssdp:discover\"",
        "MX: 2",
        "ST: ssdp:all", "", ""]).encode()

    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM, socket.IPPROTO_UDP)
    s.settimeout(timeout)
    try:
        s.setsockopt(socket.IPPROTO_IP, socket.IP_MULTICAST_TTL, 2)
    except Exception:
        pass  # not critical on some OSes
    try:
        s.sendto(msg, ("239.255.255.250", 1900))
    except Exception:
        return []

    found: Dict[str, bool] = {}
    start = time.time()
    while time.time() - start < timeout:
        try:
            data, (ip, _) = s.recvfrom(65535)
            low = data.lower()
            # Heuristic: Samsung TVs usually mention "samsung" or "DLNA" + model headers
            if b"samsung" in low or b"dlna" in low or b"upnp" in low:
                found[ip] = True
        except socket.timeout:
            break
        except Exception:
            break
    return list(found.keys())


# ------------------------------
# Device info using HTTP API
# ------------------------------

def fetch_device_info(ip: str, timeout: float = 2.0) -> dict:
    """
    Try to GET http://<ip>:8001/api/v2/ for device metadata.
    Returns {} on failure.
    """
    import urllib.request
    url = f"http://{ip}:8001/api/v2/"
    try:
        with urllib.request.urlopen(url, timeout=timeout) as resp:
            raw = resp.read()
            return json.loads(raw.decode("utf-8", errors="ignore"))
    except Exception:
        return {}


def pretty_device_line(ip: str, info: dict) -> str:
    model = info.get("device", {}).get("modelName") or info.get("device", {}).get("model") or "Unknown"
    name = info.get("device", {}).get("name") or info.get("device", {}).get("id") or "Samsung TV"
    frame = ""
    try:
        # Some firmwares expose Frame support flags, but it's not guaranteed
        if "Frame" in model or "LS03" in model:
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
    Use direct WebSocket to set auto-rotation (slideshow) because the python lib
    may not expose it on all versions.
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
    print("Samsung Frame Uploader — Interactive Wizard")
    print("=" * 72)
    print("This wizard will discover your TV, pair once, and upload a photo to Art Mode.\n")
    press_enter("Press Enter to begin…")


def step_find_tv(cfg: WizardConfig) -> bool:
    print("\n[1/10] Discover TV")
    c = prompt_choice("Choose an option:", [("1", "Scan my network for Samsung TVs (recommended)"),
                                            ("2", "Enter IP address manually")])
    if c == "1":
        print("Scanning for Samsung devices (SSDP)…")
        ips = discover_samsung_tvs(timeout=3.0)
        if not ips:
            print("No devices discovered. You can still enter the IP manually.")
            c = "2"
        else:
            labeled: List[Tuple[str, str]] = []
            for i, ip in enumerate(sorted(ips), start=1):
                info = fetch_device_info(ip)
                labeled.append((str(i), pretty_device_line(ip, info)))
            print("Found:")
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
    if c == "2":
        ip = prompt("Enter TV IP (e.g., 192.168.1.50)")
        cfg.tv_ip = ip.strip()
    # Verify device
    print("\n[2/10] Checking device…")
    info = fetch_device_info(cfg.tv_ip)
    print("• TV Info:", pretty_device_line(cfg.tv_ip, info))
    cont = prompt("Proceed with this device? (Y/n)", "Y")
    return cont.lower().startswith("y")


def step_pair(cfg: WizardConfig) -> bool:
    print("\n[3/10] Pair with TV")
    tok_file = token_path_for_ip(cfg.tv_ip)
    cfg.token_file = tok_file
    print(f"Token file: {tok_file}")
    tv = SamsungTVWS(host=cfg.tv_ip, port=8002, token_file=str(tok_file), name=cfg.client_name)
    print("The TV may show a pairing popup named “Samsung Frame Uploader”. Approve it.")
    while True:
        try:
            # Sending a benign key usually triggers pairing prompt on first run
            tv.send_key("KEY_ENTER")
            print("Paired (or already paired).")
            break
        except Exception as e:
            print(f"Waiting for approval… ({e})")
            resp = prompt("Press Enter after approving on the TV, or type 'q' to quit", "")
            if resp.lower().startswith("q"):
                return False
    return True


def step_choose_image(cfg: WizardConfig) -> bool:
    print("\n[4/10] Select image to upload")
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

    with Image.open(src) as im:
        orig_size = im.size
        if cfg.sizing_mode == "asis":
            out_path = staging_dir() / src.name
            if file_type == "JPEG":
                _save_jpeg(im, out_path)
            else:
                out_path.parent.mkdir(parents=True, exist_ok=True)
                im.save(out_path, format="PNG", optimize=True)
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
        return out_path, file_type, processed.size


def step_upload(cfg: WizardConfig) -> Optional[dict]:
    print("\n[8/10] Uploading image…")
    assert cfg.token_file is not None
    out_path, file_type, final_size = _prepare_image(cfg)
    print(f"• Prepared: {out_path.name} • {final_size[0]}x{final_size[1]} • {file_type}")

    tv = SamsungTVWS(host=cfg.tv_ip, port=8002, token_file=str(cfg.token_file), name=cfg.client_name)
    art = tv.art()
    data = Path(out_path).read_bytes()

    kwargs = {}
    if cfg.matte:
        kwargs["matte"] = cfg.matte
        # portrait matte if portrait output
        if final_size[1] > final_size[0]:
            kwargs["portrait_matte"] = cfg.matte

    try:
        resp = art.upload(data, file_type=file_type, **kwargs)
        print("• Upload OK.")
    except Exception as e:
        print(f"Upload failed: {e}")
        return None

    if cfg.ensure_artmode_on:
        try:
            art.set_artmode(True)
            print("• Art Mode set to ON.")
        except Exception:
            print("• Could not toggle Art Mode; it may already be on.")

    print("TV response:", resp)
    return resp


def step_set_current(cfg: WizardConfig, upload_resp: Optional[dict]) -> bool:
    print("\n[9/10] Show the uploaded image now?")
    print("Note: Many Frame models show the latest uploaded image automatically.")
    c = prompt_choice("Select:", [("1", "Yes"), ("2", "No")])
    if c == "2" or not upload_resp:
        return True

    # Best-effort: re-enable Art Mode (often selects last image). Some lib versions
    # expose selection methods, but to remain version-safe we'll just ensure Art Mode is on.
    try:
        tv = SamsungTVWS(host=cfg.tv_ip, port=8002, token_file=str(cfg.token_file), name=cfg.client_name)
        tv.art().set_artmode(True)
        print("• Ensured Art Mode ON (should show the latest upload).")
    except Exception:
        print("• Could not explicitly switch image; select it from Art Mode if needed.")
    return True


def step_slideshow(cfg: WizardConfig) -> bool:
    print("\n[10/10] Configure slideshow (optional)")
    c = prompt_choice("Choose:", [
        ("1", "Off"),
        ("2", "Every 60 minutes (serial)"),
        ("3", "Every 1440 minutes (daily)"),
        ("4", "Shuffle every 60 minutes"),
    ])
    cfg.slideshow = {"1": "off", "2": "60", "3": "1440", "4": "shuffle60"}[c]

    if cfg.slideshow:
        token = ""
        try:
            token = Path(cfg.token_file).read_text().strip() if cfg.token_file else ""
        except Exception:
            pass

        if not token:
            print("• Warning: Could not read token for slideshow config.")
            return True

        minutes = "off" if cfg.slideshow == "off" else ("60" if cfg.slideshow.endswith("60") else "1440")
        mode = "serial" if cfg.slideshow in ("off", "60", "1440") else "shuffle"
        try:
            resp = set_slideshow_over_ws(cfg.tv_ip, token, minutes=minutes, mode=mode)
            print("• Slideshow applied:", resp)
        except Exception as e:
            print("• Could not set slideshow:", e)
    return True


def step_save_profile(cfg: WizardConfig) -> None:
    print("\nSave defaults for next run?")
    c = prompt_choice("Save profile?", [("1", "Yes"), ("2", "No")])
    if c == "2":
        print("Done!")
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
    print(f"Saved profile '{name}'. Next time you can reuse these defaults.")


def main():
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
    step_set_current(cfg, upload_resp)
    step_slideshow(cfg)
    step_save_profile(cfg)
    print("\nDone! Your photo should now be on The Frame.")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nAborted by user.")
