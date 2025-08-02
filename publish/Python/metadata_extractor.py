"""
Comprehensive metadata extraction module for MyPhotoHelper.

This module extracts detailed metadata from images including:
- Basic image properties (dimensions, color space, etc.)
- Camera information (make, model, settings)
- GPS location data with altitude
- Date/time information
- Technical camera settings (ISO, aperture, focal length, etc.)
- Author and copyright information
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
    Extract comprehensive metadata from an image file.
    
    Args:
        image_path: Path to the image file
        
    Returns:
        Dictionary containing comprehensive metadata
    """
    # Initialize result with all possible fields
    result = {
        # Basic Image Properties
        "width": 0,
        "height": 0,
        "color_space": None,
        "bit_depth": None,
        "orientation": None,
        "resolution_x": None,
        "resolution_y": None,
        "resolution_unit": None,
        
        # Date/Time Information
        "date_taken": None,
        "date_digitized": None,
        "date_modified": None,
        "time_zone": None,
        
        # GPS/Location Data
        "latitude": None,
        "longitude": None,
        "altitude": None,
        "gps_direction": None,
        "gps_speed": None,
        "gps_processing_method": None,
        "location_name": None,
        
        # Camera Information
        "camera_make": None,
        "camera_model": None,
        "camera_serial": None,
        "lens_model": None,
        "lens_make": None,
        "lens_serial": None,
        
        # Camera Settings
        "focal_length": None,
        "focal_length_35mm": None,
        "f_number": None,
        "exposure_time": None,
        "iso": None,
        "exposure_mode": None,
        "exposure_program": None,
        "metering_mode": None,
        "flash": None,
        "white_balance": None,
        "scene_capture_type": None,
        
        # Software/Processing
        "software": None,
        "processing_software": None,
        "artist": None,
        "copyright": None,
        
        # Technical Details
        "color_profile": None,
        "exposure_bias": None,
        "max_aperture": None,
        "subject_distance": None,
        "light_source": None,
        "sensing_method": None,
        "file_source": None,
        "scene_type": None,
        
        # Additional Properties
        "image_description": None,
        "user_comment": None,
        "keywords": None,
        "subject": None,
        
        # Processing Info
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
            
            # Get color mode/space
            result["color_space"] = img.mode
            
            # Try to get EXIF data
            exif_data = img.getexif()
            if exif_data:
                _extract_basic_metadata(exif_data, result)
                _extract_camera_metadata(exif_data, result)
                _extract_technical_metadata(exif_data, result)
                _extract_gps_metadata(exif_data, result)
                _extract_datetime_metadata(exif_data, result)
                _extract_additional_metadata(exif_data, result)
            
    except Exception as e:
        result["error"] = f"Error extracting metadata: {str(e)}"
        
    return result


def _extract_basic_metadata(exif_data, result):
    """Extract basic image metadata."""
    try:
        # Resolution
        if 0x011A in exif_data:  # XResolution
            result["resolution_x"] = float(exif_data[0x011A])
        if 0x011B in exif_data:  # YResolution
            result["resolution_y"] = float(exif_data[0x011B])
        if 0x0128 in exif_data:  # ResolutionUnit
            unit_map = {1: "None", 2: "inches", 3: "cm"}
            result["resolution_unit"] = unit_map.get(exif_data[0x0128], str(exif_data[0x0128]))
            
        # Orientation
        if 0x0112 in exif_data:  # Orientation
            orientation_map = {
                1: "Normal", 2: "Flipped horizontally", 3: "Rotated 180°",
                4: "Flipped vertically", 5: "Rotated 90° CCW, flipped horizontally",
                6: "Rotated 90° CW", 7: "Rotated 90° CW, flipped horizontally",
                8: "Rotated 90° CCW"
            }
            result["orientation"] = orientation_map.get(exif_data[0x0112], str(exif_data[0x0112]))
            
        # Color space
        if 0xA001 in exif_data:  # ColorSpace
            color_space_map = {1: "sRGB", 65535: "Uncalibrated"}
            result["color_profile"] = color_space_map.get(exif_data[0xA001], str(exif_data[0xA001]))
            
    except Exception:
        pass


def _extract_camera_metadata(exif_data, result):
    """Extract camera-specific metadata."""
    try:
        # Camera make and model
        if 0x010F in exif_data:  # Make
            result["camera_make"] = str(exif_data[0x010F])
        if 0x0110 in exif_data:  # Model
            result["camera_model"] = str(exif_data[0x0110])
        if 0xA431 in exif_data:  # BodySerialNumber
            result["camera_serial"] = str(exif_data[0xA431])
            
        # Lens information
        if 0xA434 in exif_data:  # LensModel
            result["lens_model"] = str(exif_data[0xA434])
        if 0xA433 in exif_data:  # LensMake
            result["lens_make"] = str(exif_data[0xA433])
        if 0xA435 in exif_data:  # LensSerialNumber
            result["lens_serial"] = str(exif_data[0xA435])
            
    except Exception:
        pass


def _extract_technical_metadata(exif_data, result):
    """Extract technical camera settings."""
    try:
        # Focal length
        if 0x920A in exif_data:  # FocalLength
            result["focal_length"] = float(exif_data[0x920A])
        if 0xA405 in exif_data:  # FocalLengthIn35mmFilm
            result["focal_length_35mm"] = float(exif_data[0xA405])
            
        # Aperture
        if 0x829D in exif_data:  # FNumber
            result["f_number"] = f"f/{float(exif_data[0x829D]):.1f}"
        if 0x9202 in exif_data:  # ApertureValue or MaxApertureValue
            result["max_aperture"] = float(exif_data[0x9202])
            
        # Exposure
        if 0x829A in exif_data:  # ExposureTime
            exp_time = float(exif_data[0x829A])
            if exp_time >= 1:
                result["exposure_time"] = f"{exp_time:.1f}s"
            else:
                result["exposure_time"] = f"1/{int(1/exp_time)}s"
                
        # ISO
        if 0x8827 in exif_data:  # ISO
            result["iso"] = int(exif_data[0x8827])
            
        # Exposure bias
        if 0x9204 in exif_data:  # ExposureBiasValue
            result["exposure_bias"] = float(exif_data[0x9204])
            
        # Exposure mode
        if 0xA402 in exif_data:  # ExposureMode
            exp_mode_map = {0: "Auto", 1: "Manual", 2: "Auto bracket"}
            result["exposure_mode"] = exp_mode_map.get(exif_data[0xA402], str(exif_data[0xA402]))
            
        # Exposure program
        if 0x8822 in exif_data:  # ExposureProgram
            exp_prog_map = {
                0: "Not defined", 1: "Manual", 2: "Normal program",
                3: "Aperture priority", 4: "Shutter priority", 5: "Creative program",
                6: "Action program", 7: "Portrait mode", 8: "Landscape mode"
            }
            result["exposure_program"] = exp_prog_map.get(exif_data[0x8822], str(exif_data[0x8822]))
            
        # Metering mode
        if 0x9207 in exif_data:  # MeteringMode
            meter_map = {
                0: "Unknown", 1: "Average", 2: "Center-weighted average",
                3: "Spot", 4: "Multi-spot", 5: "Pattern", 6: "Partial", 255: "Other"
            }
            result["metering_mode"] = meter_map.get(exif_data[0x9207], str(exif_data[0x9207]))
            
        # Flash
        if 0x9209 in exif_data:  # Flash
            flash_value = exif_data[0x9209]
            flash_fired = "Yes" if (flash_value & 0x01) else "No"
            result["flash"] = f"Flash fired: {flash_fired}"
            
        # White balance
        if 0xA403 in exif_data:  # WhiteBalance
            wb_map = {0: "Auto", 1: "Manual"}
            result["white_balance"] = wb_map.get(exif_data[0xA403], str(exif_data[0xA403]))
            
        # Scene capture type
        if 0xA406 in exif_data:  # SceneCaptureType
            scene_map = {0: "Standard", 1: "Landscape", 2: "Portrait", 3: "Night scene"}
            result["scene_capture_type"] = scene_map.get(exif_data[0xA406], str(exif_data[0xA406]))
            
        # Subject distance
        if 0x9206 in exif_data:  # SubjectDistance
            result["subject_distance"] = f"{float(exif_data[0x9206]):.2f}m"
            
        # Light source
        if 0x9208 in exif_data:  # LightSource
            light_map = {
                0: "Unknown", 1: "Daylight", 2: "Fluorescent", 3: "Tungsten",
                4: "Flash", 9: "Fine weather", 10: "Cloudy weather", 11: "Shade",
                12: "Daylight fluorescent", 13: "Day white fluorescent",
                14: "Cool white fluorescent", 15: "White fluorescent", 17: "Standard light A",
                18: "Standard light B", 19: "Standard light C", 20: "D55", 21: "D65",
                22: "D75", 23: "D50", 24: "ISO studio tungsten"
            }
            result["light_source"] = light_map.get(exif_data[0x9208], str(exif_data[0x9208]))
            
    except Exception:
        pass


def _extract_gps_metadata(exif_data, result):
    """Extract GPS and location metadata."""
    try:
        # Extract GPS coordinates
        gps_info = exif_data.get_ifd(0x8825)  # GPS IFD
        if gps_info:
            lat, lon = _extract_gps_coordinates(gps_info)
            if lat is not None and lon is not None:
                result["latitude"] = lat
                result["longitude"] = lon
                result["has_gps"] = True
                
            # GPS Altitude
            if 6 in gps_info:  # GPSAltitude
                altitude = float(gps_info[6])
                altitude_ref = gps_info.get(5, 0)  # GPSAltitudeRef (0 = above sea level, 1 = below)
                if altitude_ref == 1:
                    altitude = -altitude
                result["altitude"] = altitude
                
            # GPS Direction
            if 17 in gps_info:  # GPSImgDirection
                result["gps_direction"] = f"{float(gps_info[17]):.1f}°"
                
            # GPS Speed
            if 13 in gps_info:  # GPSSpeed
                result["gps_speed"] = float(gps_info[13])
                
            # GPS Processing Method
            if 27 in gps_info:  # GPSProcessingMethod
                result["gps_processing_method"] = str(gps_info[27])
                
    except Exception:
        pass


def _extract_datetime_metadata(exif_data, result):
    """Extract date and time metadata."""
    try:
        # Date taken (original)
        if 0x9003 in exif_data:  # DateTimeOriginal
            date_str = exif_data[0x9003]
            try:
                date_obj = datetime.strptime(date_str, "%Y:%m:%d %H:%M:%S")
                result["date_taken"] = date_obj.isoformat()
            except:
                result["date_taken"] = date_str
                
        # Date digitized
        if 0x9004 in exif_data:  # DateTimeDigitized
            date_str = exif_data[0x9004]
            try:
                date_obj = datetime.strptime(date_str, "%Y:%m:%d %H:%M:%S")
                result["date_digitized"] = date_obj.isoformat()
            except:
                result["date_digitized"] = date_str
                
        # Date modified
        if 0x0132 in exif_data:  # DateTime
            date_str = exif_data[0x0132]
            try:
                date_obj = datetime.strptime(date_str, "%Y:%m:%d %H:%M:%S")
                result["date_modified"] = date_obj.isoformat()
            except:
                result["date_modified"] = date_str
                
        # Timezone
        if 0x9010 in exif_data:  # OffsetTime
            result["time_zone"] = str(exif_data[0x9010])
            
    except Exception:
        pass


def _extract_additional_metadata(exif_data, result):
    """Extract additional metadata like software, author, etc."""
    try:
        # Software
        if 0x0131 in exif_data:  # Software
            result["software"] = str(exif_data[0x0131])
            
        # Artist/Author
        if 0x013B in exif_data:  # Artist
            result["artist"] = str(exif_data[0x013B])
            
        # Copyright
        if 0x8298 in exif_data:  # Copyright
            result["copyright"] = str(exif_data[0x8298])
            
        # Image description
        if 0x010E in exif_data:  # ImageDescription
            result["image_description"] = str(exif_data[0x010E])
            
        # User comment
        if 0x9286 in exif_data:  # UserComment
            result["user_comment"] = str(exif_data[0x9286])
            
        # Keywords (if present in EXIF)
        if 0x9C9D in exif_data:  # Keywords
            result["keywords"] = str(exif_data[0x9C9D])
            
        # Subject
        if 0x9C9F in exif_data:  # Subject
            result["subject"] = str(exif_data[0x9C9F])
            
    except Exception:
        pass


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


# Interface functions for backwards compatibility
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