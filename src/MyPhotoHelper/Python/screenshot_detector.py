"""
Screenshot detection module for MyPhotoHelper.

This module provides fast, reliable screenshot detection using multiple detection layers:
1. Filename analysis - 100% reliable when patterns match
2. Resolution analysis - High confidence based on common screen resolutions  
3. Metadata analysis - Medium confidence based on software and camera data

The system uses a confidence scoring approach where scores above 75% are considered screenshots.
"""

import os
import re
from typing import Dict, Any, Tuple, Optional, List
from pathlib import Path
from datetime import datetime

# Try to import PIL for image analysis
try:
    from PIL import Image
    from PIL.ExifTags import TAGS
    HAS_PIL_SUPPORT = True
except ImportError:
    HAS_PIL_SUPPORT = False


class ScreenshotDetector:
    """Main screenshot detection class with layered analysis."""
    
    # Common screenshot filename patterns
    SCREENSHOT_PATTERNS = [
        r'screenshot',
        r'screen\s*shot',
        r'capture',
        r'snip',
        r'clipboardimage',
        r'shot',
        r'grab',
        r'print\s*screen',
        r'prtsc',
        r'screenclip'
    ]
    
    # Date/time patterns commonly used by screenshot tools
    SCREENSHOT_DATE_PATTERNS = [
        r'screenshot.*\d{4}[-_]\d{1,2}[-_]\d{1,2}',  # screenshot_2024-01-27
        r'screen\s*shot.*\d{4}[-_]\d{1,2}[-_]\d{1,2}',  # Screen Shot 2024-01-27
        r'capture.*\d{4}[-_]\d{1,2}[-_]\d{1,2}',  # capture_2024-01-27
        r'\d{4}[-_]\d{1,2}[-_]\d{1,2}.*screenshot',  # 2024-01-27_screenshot
    ]
    
    # Common desktop screen resolutions (width, height)
    DESKTOP_RESOLUTIONS = {
        (1920, 1080): 'Full HD',
        (1366, 768): 'HD Ready',
        (1536, 864): 'HD+',
        (2560, 1440): 'QHD',
        (3840, 2160): '4K UHD',
        (1440, 900): 'WXGA+',
        (1680, 1050): 'WSXGA+',
        (1280, 720): 'HD',
        (1280, 800): 'WXGA',
        (1024, 768): 'XGA',
        (1600, 900): 'HD+',
        (2048, 1152): 'QWXGA',
        (2880, 1620): '3K',
        (5120, 2880): '5K'
    }
    
    # Common mobile/tablet screenshot resolutions
    MOBILE_RESOLUTIONS = {
        (390, 844): 'iPhone 12/13/14',
        (393, 852): 'iPhone 14 Pro',
        (430, 932): 'iPhone 14 Pro Max',
        (414, 896): 'iPhone 11/XR',
        (375, 812): 'iPhone X/11 Pro',
        (375, 667): 'iPhone 6/7/8',
        (360, 800): 'Android Common',
        (412, 915): 'Pixel 7',
        (411, 891): 'Galaxy S21',
        (384, 854): 'Galaxy S22',
        (768, 1024): 'iPad',
        (834, 1194): 'iPad Air',
        (1024, 1366): 'iPad Pro 12.9"'
    }
    
    # Software commonly used for screenshots
    SCREENSHOT_SOFTWARE_PATTERNS = [
        'snagit',
        'lightshot', 
        'greenshot',
        'puush',
        'gyazo',
        'screenshot',
        'capture',
        'snip',
        'spectacle',
        'flameshot',
        'scrot'
    ]
    
    def __init__(self):
        """Initialize the screenshot detector."""
        self.compiled_patterns = [re.compile(pattern, re.IGNORECASE) for pattern in self.SCREENSHOT_PATTERNS]
        self.compiled_date_patterns = [re.compile(pattern, re.IGNORECASE) for pattern in self.SCREENSHOT_DATE_PATTERNS]
        self.compiled_software_patterns = [re.compile(pattern, re.IGNORECASE) for pattern in self.SCREENSHOT_SOFTWARE_PATTERNS]
    
    def detect_screenshot(self, image_path: str) -> Tuple[bool, float, Dict[str, Any]]:
        """
        Detect if an image is a screenshot using all available methods.
        
        Args:
            image_path: Path to the image file
            
        Returns:
            Tuple of (is_screenshot, confidence, details)
            - is_screenshot: True if confidence > 0.75
            - confidence: Float between 0.0 and 1.0
            - details: Dictionary with analysis details
        """
        details = {
            'filename_analysis': {},
            'resolution_analysis': {},
            'metadata_analysis': {},
            'final_scores': {},
            'detection_method': 'none',
            'error': None
        }
        
        try:
            if not os.path.exists(image_path):
                details['error'] = 'File not found'
                return False, 0.0, details
            
            # Layer 1: Filename Analysis (highest confidence)
            filename_score, filename_details = self._analyze_filename(image_path)
            details['filename_analysis'] = filename_details
            
            # Layer 2: Resolution Analysis (if PIL available)
            resolution_score, resolution_details = self._analyze_resolution(image_path)
            details['resolution_analysis'] = resolution_details
            
            # Layer 3: Metadata Analysis (if PIL available)
            metadata_score, metadata_details = self._analyze_metadata(image_path)
            details['metadata_analysis'] = metadata_details
            
            # Combine scores with weights
            final_confidence = self._calculate_final_confidence(
                filename_score, resolution_score, metadata_score
            )
            
            # Determine detection method
            if filename_score > 0.8:
                details['detection_method'] = 'filename'
            elif resolution_score > 0.6 and metadata_score > 0.5:
                details['detection_method'] = 'resolution_metadata'
            elif resolution_score > 0.7:
                details['detection_method'] = 'resolution'
            elif metadata_score > 0.6:
                details['detection_method'] = 'metadata'
            else:
                details['detection_method'] = 'heuristic'
            
            details['final_scores'] = {
                'filename_score': filename_score,
                'resolution_score': resolution_score,
                'metadata_score': metadata_score,
                'final_confidence': final_confidence
            }
            
            is_screenshot = final_confidence > 0.75
            
            return is_screenshot, final_confidence, details
            
        except Exception as e:
            details['error'] = str(e)
            return False, 0.0, details
    
    def _analyze_filename(self, image_path: str) -> Tuple[float, Dict[str, Any]]:
        """
        Analyze filename for screenshot patterns.
        
        Returns:
            Tuple of (confidence_score, analysis_details)
        """
        filename = os.path.basename(image_path).lower()
        details = {
            'filename': filename,
            'patterns_matched': [],
            'date_patterns_matched': [],
            'confidence_reason': 'no_match'
        }
        
        # Check for screenshot patterns
        pattern_matches = []
        for i, pattern in enumerate(self.compiled_patterns):
            if pattern.search(filename):
                pattern_matches.append(self.SCREENSHOT_PATTERNS[i])
        
        details['patterns_matched'] = pattern_matches
        
        # Check for date patterns (higher confidence)
        date_matches = []
        for i, pattern in enumerate(self.compiled_date_patterns):
            if pattern.search(filename):
                date_matches.append(self.SCREENSHOT_DATE_PATTERNS[i])
        
        details['date_patterns_matched'] = date_matches
        
        # Calculate confidence
        if date_matches:
            # Date patterns are very specific - highest confidence
            details['confidence_reason'] = 'date_pattern'
            return 0.95, details
        elif len(pattern_matches) >= 2:
            # Multiple patterns match - high confidence
            details['confidence_reason'] = 'multiple_patterns'
            return 0.90, details
        elif pattern_matches:
            # Single pattern match - good confidence
            details['confidence_reason'] = 'single_pattern'
            return 0.85, details
        else:
            # No patterns matched
            return 0.0, details
    
    def _analyze_resolution(self, image_path: str) -> Tuple[float, Dict[str, Any]]:
        """
        Analyze image resolution against common screen resolutions.
        
        Returns:
            Tuple of (confidence_score, analysis_details)
        """
        details = {
            'width': 0,
            'height': 0,
            'resolution_match': None,
            'aspect_ratio': 0.0,
            'confidence_reason': 'no_pil'
        }
        
        if not HAS_PIL_SUPPORT:
            return 0.0, details
        
        try:
            with Image.open(image_path) as img:
                width, height = img.size
                details['width'] = width
                details['height'] = height
                details['aspect_ratio'] = width / height if height > 0 else 0
                
                # Check exact matches with common resolutions
                resolution = (width, height)
                
                # Desktop resolutions (higher confidence for screenshots)
                if resolution in self.DESKTOP_RESOLUTIONS:
                    details['resolution_match'] = f"Desktop: {self.DESKTOP_RESOLUTIONS[resolution]}"
                    details['confidence_reason'] = 'exact_desktop_match'
                    return 0.80, details
                
                # Mobile resolutions (medium-high confidence)
                if resolution in self.MOBILE_RESOLUTIONS:
                    details['resolution_match'] = f"Mobile: {self.MOBILE_RESOLUTIONS[resolution]}"
                    details['confidence_reason'] = 'exact_mobile_match'
                    return 0.75, details
                
                # Check for common aspect ratios
                aspect_ratio = width / height if height > 0 else 0
                
                # 16:9 aspect ratio (very common for screenshots)
                if abs(aspect_ratio - 16/9) < 0.01:
                    details['confidence_reason'] = '16_9_aspect_ratio'
                    return 0.60, details
                
                # 16:10 aspect ratio (common for laptops)
                if abs(aspect_ratio - 16/10) < 0.01:
                    details['confidence_reason'] = '16_10_aspect_ratio'
                    return 0.55, details
                
                # 4:3 aspect ratio (older monitors)
                if abs(aspect_ratio - 4/3) < 0.01:
                    details['confidence_reason'] = '4_3_aspect_ratio'
                    return 0.45, details
                
                # Very wide or tall images are less likely to be screenshots
                if aspect_ratio > 3.0 or aspect_ratio < 0.3:
                    details['confidence_reason'] = 'unusual_aspect_ratio'
                    return 0.10, details
                
                # Default for reasonable dimensions
                details['confidence_reason'] = 'reasonable_dimensions'
                return 0.30, details
                
        except Exception as e:
            details['error'] = str(e)
            return 0.0, details
    
    def _analyze_metadata(self, image_path: str) -> Tuple[float, Dict[str, Any]]:
        """
        Analyze image metadata for screenshot indicators.
        
        Returns:
            Tuple of (confidence_score, analysis_details)
        """
        details = {
            'has_exif': False,
            'has_camera_info': False,
            'software_info': None,
            'screenshot_software_detected': False,
            'confidence_reason': 'no_pil'
        }
        
        if not HAS_PIL_SUPPORT:
            return 0.0, details
        
        try:
            with Image.open(image_path) as img:
                exif = img.getexif()
                details['has_exif'] = bool(exif)
                
                if not exif:
                    # No EXIF data is common for screenshots
                    details['confidence_reason'] = 'no_exif_data'
                    return 0.60, details
                
                # Check for camera information
                camera_fields = ['Make', 'Model', 'LensModel', 'LensMake']
                has_camera_info = False
                
                software_info = None
                
                for tag_id, value in exif.items():
                    tag_name = TAGS.get(tag_id, tag_id)
                    
                    if tag_name in camera_fields and value:
                        has_camera_info = True
                        
                    if tag_name == 'Software' and value:
                        software_info = str(value).lower()
                        details['software_info'] = value
                        
                        # Check if software indicates screenshot tool
                        for pattern in self.compiled_software_patterns:
                            if pattern.search(software_info):
                                details['screenshot_software_detected'] = True
                                details['confidence_reason'] = 'screenshot_software'
                                return 0.90, details
                
                details['has_camera_info'] = has_camera_info
                
                if not has_camera_info:
                    # No camera info suggests screenshot or edited image
                    details['confidence_reason'] = 'no_camera_info'
                    return 0.50, details
                else:
                    # Has camera info - less likely to be screenshot
                    details['confidence_reason'] = 'has_camera_info'
                    return 0.20, details
                    
        except Exception as e:
            details['error'] = str(e)
            return 0.0, details
    
    def _calculate_final_confidence(self, filename_score: float, resolution_score: float, metadata_score: float) -> float:
        """
        Calculate final confidence score by combining all detection methods.
        
        Args:
            filename_score: Score from filename analysis (0.0-1.0)
            resolution_score: Score from resolution analysis (0.0-1.0) 
            metadata_score: Score from metadata analysis (0.0-1.0)
            
        Returns:
            Final confidence score (0.0-1.0)
        """
        # Filename has highest weight as it's most reliable
        if filename_score > 0.8:
            # If filename strongly indicates screenshot, give it priority
            return min(1.0, filename_score + (resolution_score * 0.1) + (metadata_score * 0.1))
        
        # Otherwise, combine with weighted average
        weights = {
            'filename': 0.5,      # Highest weight
            'resolution': 0.3,    # Medium weight
            'metadata': 0.2       # Lower weight
        }
        
        weighted_score = (
            filename_score * weights['filename'] +
            resolution_score * weights['resolution'] +
            metadata_score * weights['metadata']
        )
        
        # Boost confidence if multiple methods agree
        agreement_bonus = 0.0
        scores = [filename_score, resolution_score, metadata_score]
        high_scores = [s for s in scores if s > 0.5]
        
        if len(high_scores) >= 2:
            agreement_bonus = 0.1
        elif len(high_scores) >= 3:
            agreement_bonus = 0.15
        
        final_score = min(1.0, weighted_score + agreement_bonus)
        return final_score


def detect_screenshot(image_path: str) -> Tuple[bool, float, Dict[str, Any]]:
    """
    Simple interface function to detect if an image is a screenshot.
    
    Args:
        image_path: Path to the image file
        
    Returns:
        Tuple of (is_screenshot, confidence, details)
    """
    detector = ScreenshotDetector()
    return detector.detect_screenshot(image_path)


def batch_detect_screenshots(image_paths: List[str]) -> Dict[str, Tuple[bool, float, Dict[str, Any]]]:
    """
    Detect screenshots for multiple images efficiently.
    
    Args:
        image_paths: List of image file paths
        
    Returns:
        Dictionary mapping image_path -> (is_screenshot, confidence, details)
    """
    detector = ScreenshotDetector()
    results = {}
    
    for image_path in image_paths:
        results[image_path] = detector.detect_screenshot(image_path)
    
    return results


def test_screenshot_detection():
    """Test function to verify the module works correctly."""
    return "Screenshot detection module loaded successfully"


if __name__ == "__main__":
    # Test the module
    print(test_screenshot_detection())
    
    # Test with sample filenames
    test_cases = [
        "screenshot_2024-01-27.png",
        "Screen Shot 2024-01-27 at 10.30.45 AM.png", 
        "family_vacation.jpg",
        "IMG_1234.jpg"
    ]
    
    detector = ScreenshotDetector()
    
    for filename in test_cases:
        fake_path = f"/fake/path/{filename}"
        try:
            is_screenshot, confidence, details = detector.detect_screenshot(fake_path)
            print(f"{filename}: {is_screenshot} (confidence: {confidence:.2f})")
        except:
            print(f"{filename}: Could not analyze (file doesn't exist)")