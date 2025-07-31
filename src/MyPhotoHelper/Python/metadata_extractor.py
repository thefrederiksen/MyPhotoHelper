"""
Metadata extraction module for MyPhotoHelper.

This module provides methods to extract metadata (including GPS coordinates)
from various image formats including JPEG and HEIC/HEIF.
"""

import os
from typing import Optional, Tuple, Dict, Any
from datetime import datetime
from pathlib import Path

# Try to import PIL/Pillow with HEIC support
try:
    from PIL import Image
    from PIL.ExifTags import TAGS, GPSTAGS
    from pillow_heif import register_heif_opener
    
    # Register HEIF opener with Pillow
    register_heif_opener()
    HAS_PIL_SUPPORT = True
except ImportError:
    HAS_PIL_SUPPORT = False


def extract_image_metadata(image_path: str) -> Dict[str, Any]:
    """
    Extract metadata from an image file.
    
    Args:
        image_path: Path to the image file
        
    Returns:
        Dictionary containing metadata:
        - width: Image width in pixels
        - height: Image height in pixels
        - date_taken: Date the photo was taken (if available)
        - latitude: GPS latitude (if available)
        - longitude: GPS longitude (if available)
        - has_gps: Boolean indicating if GPS data is present
        - error: Error message if extraction failed
    """
    result = {
        "width": 0,
        "height": 0,
        "date_taken": None,
        "latitude": None,
        "longitude": None,
        "has_gps": False,
        "error": None
    }
    
    if not HAS_PIL_SUPPORT:
        result["error"] = "PIL/Pillow not available"
        return result
        
    try:
        if not os.path.exists(image_path):
            result["error"] = f"File not found: {image_path}"
            return result
            
        # Open image with Pillow (works for both JPEG and HEIC)
        with Image.open(image_path) as img:
            # Get basic dimensions
            result["width"] = img.width
            result["height"] = img.height
            
            # Try to get EXIF data
            exif_data = img.getexif()
            if exif_data:
                # Extract date taken
                date_taken = _extract_date_taken(exif_data)
                if date_taken:
                    result["date_taken"] = date_taken.isoformat()
                
                # Extract GPS coordinates
                gps_info = exif_data.get_ifd(0x8825)  # GPS IFD
                if gps_info:
                    lat, lon = _extract_gps_coordinates(gps_info)
                    if lat is not None and lon is not None:
                        result["latitude"] = lat
                        result["longitude"] = lon
                        result["has_gps"] = True
            
    except Exception as e:
        result["error"] = f"Error extracting metadata: {str(e)}"
        
    return result


def _extract_date_taken(exif_data) -> Optional[datetime]:
    """Extract date taken from EXIF data."""
    # Try different date tags
    date_tags = [
        0x9003,  # DateTimeOriginal
        0x9004,  # DateTimeDigitized
        0x0132   # DateTime
    ]
    
    for tag in date_tags:
        if tag in exif_data:
            try:
                date_str = exif_data[tag]
                # EXIF date format: "YYYY:MM:DD HH:MM:SS"
                return datetime.strptime(date_str, "%Y:%m:%d %H:%M:%S")
            except:
                continue
                
    return None


def _extract_gps_coordinates(gps_info: Dict) -> Tuple[Optional[float], Optional[float]]:
    """Extract GPS coordinates from GPS IFD data."""
    try:
        # GPS tags
        gps_latitude_ref = gps_info.get(1)  # GPSLatitudeRef (N/S)
        gps_latitude = gps_info.get(2)      # GPSLatitude
        gps_longitude_ref = gps_info.get(3) # GPSLongitudeRef (E/W)
        gps_longitude = gps_info.get(4)     # GPSLongitude
        
        if not all([gps_latitude, gps_longitude]):
            return None, None
            
        # Convert GPS coordinates to decimal degrees
        lat = _convert_to_degrees(gps_latitude)
        lon = _convert_to_degrees(gps_longitude)
        
        # Apply hemisphere modifiers
        if gps_latitude_ref == 'S':
            lat = -lat
        if gps_longitude_ref == 'W':
            lon = -lon
            
        return lat, lon
        
    except Exception:
        return None, None


def _convert_to_degrees(value) -> float:
    """Convert GPS coordinate to decimal degrees."""
    # GPS coordinates are stored as ((degrees, 1), (minutes, 1), (seconds, divisor))
    d, m, s = value
    degrees = float(d)
    minutes = float(m) / 60.0
    seconds = float(s) / 3600.0
    return degrees + minutes + seconds


def check_metadata_support() -> Dict[str, bool]:
    """Check if metadata extraction is available."""
    result = {
        "has_pil_support": HAS_PIL_SUPPORT,
        "can_extract_jpeg": False,
        "can_extract_heic": False
    }
    
    if HAS_PIL_SUPPORT:
        result["can_extract_jpeg"] = True
        # Check if HEIC is supported
        try:
            from pillow_heif import HeifImagePlugin
            result["can_extract_heic"] = True
        except ImportError:
            pass
            
    return result


def get_supported_formats() -> list:
    """Get list of supported image formats for metadata extraction."""
    formats = []
    
    if HAS_PIL_SUPPORT:
        # Basic formats always supported by PIL
        formats.extend(['.jpg', '.jpeg', '.png', '.bmp', '.gif', '.tiff', '.webp'])
        
        # Check if HEIC is supported
        try:
            from pillow_heif import HeifImagePlugin
            formats.extend(['.heic', '.heif'])
        except ImportError:
            pass
            
    return formats


# Simple interface functions for C# interop
def extract_metadata(image_path: str) -> str:
    """Extract metadata and return as JSON string."""
    import json
    result = extract_image_metadata(image_path)
    return json.dumps(result)


def has_gps_coordinates(image_path: str) -> bool:
    """Check if image has GPS coordinates."""
    metadata = extract_image_metadata(image_path)
    return metadata.get("has_gps", False)


def get_image_dimensions(image_path: str) -> Tuple[int, int]:
    """Get image dimensions as tuple (width, height)."""
    metadata = extract_image_metadata(image_path)
    return metadata.get("width", 0), metadata.get("height", 0)