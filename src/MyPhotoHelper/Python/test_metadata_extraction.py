#!/usr/bin/env python3
"""
Test script for comprehensive metadata extraction
Tests the new metadata_extractor module with real images
"""

import os
import sys
from pathlib import Path

# Add the current directory to path so we can import our module
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from metadata_extractor import extract_image_metadata, check_metadata_support

def test_metadata_extraction():
    """Test metadata extraction with real images"""
    
    # Check if metadata extraction is supported
    support = check_metadata_support()
    print("=== Metadata Extraction Support ===")
    print(f"PIL Support: {support['has_pil_support']}")
    print(f"JPEG Support: {support['can_extract_jpeg']}")
    print(f"HEIC Support: {support['can_extract_heic']}")
    print()
    
    # Test images path
    test_images_dir = Path(__file__).parent.parent.parent / "MyPhotoHelper.Tests" / "Images" / "Coordinates"
    
    if not test_images_dir.exists():
        print(f"Test images directory not found: {test_images_dir}")
        return
    
    # Find all test images
    test_images = list(test_images_dir.glob("*"))
    
    if not test_images:
        print(f"No test images found in: {test_images_dir}")
        return
    
    print(f"Found {len(test_images)} test images in: {test_images_dir}")
    print()
    
    # Test each image
    for image_path in test_images:
        print(f"{'='*60}")
        print(f"Testing: {image_path.name}")
        print(f"{'='*60}")
        
        # Extract metadata
        metadata = extract_image_metadata(str(image_path))
        
        # Check for errors
        if metadata.get("error"):
            print(f"ERROR: {metadata['error']}")
            continue
        
        # Print basic properties
        print("\n[Basic Properties]")
        print(f"  Dimensions: {metadata['width']} x {metadata['height']}")
        print(f"  Color Space: {metadata['color_space']}")
        print(f"  Orientation: {metadata['orientation']}")
        print(f"  Resolution: {metadata['resolution_x']} x {metadata['resolution_y']} {metadata['resolution_unit']}")
        
        # Print camera information
        print("\n[Camera Information]")
        print(f"  Make: {metadata['camera_make']}")
        print(f"  Model: {metadata['camera_model']}")
        print(f"  Lens: {metadata['lens_model']}")
        
        # Print camera settings
        print("\n[Camera Settings]")
        print(f"  Focal Length: {metadata['focal_length']}mm")
        print(f"  Aperture: {metadata['f_number']}")
        print(f"  Exposure: {metadata['exposure_time']}")
        print(f"  ISO: {metadata['iso']}")
        print(f"  Flash: {metadata['flash']}")
        print(f"  White Balance: {metadata['white_balance']}")
        
        # Print GPS data
        print("\n[GPS/Location Data]")
        if metadata['has_gps']:
            print(f"  Latitude: {metadata['latitude']}")
            print(f"  Longitude: {metadata['longitude']}")
            print(f"  Altitude: {metadata['altitude']}m")
            print(f"  Direction: {metadata['gps_direction']}")
            print(f"  GPS Method: {metadata['gps_processing_method']}")
        else:
            print("  No GPS data found")
        
        # Print dates
        print("\n[Date/Time Information]")
        print(f"  Date Taken: {metadata['date_taken']}")
        print(f"  Date Digitized: {metadata['date_digitized']}")
        print(f"  Date Modified: {metadata['date_modified']}")
        print(f"  Time Zone: {metadata['time_zone']}")
        
        # Print software info
        print("\n[Software/Processing]")
        print(f"  Software: {metadata['software']}")
        print(f"  Artist: {metadata['artist']}")
        print(f"  Copyright: {metadata['copyright']}")
        
        # Count how many fields have values
        non_null_fields = sum(1 for k, v in metadata.items() 
                             if v is not None and k not in ['error', 'has_gps'])
        print(f"\n[Summary]")
        print(f"  Fields with data: {non_null_fields}/52")
        print()


if __name__ == "__main__":
    test_metadata_extraction()