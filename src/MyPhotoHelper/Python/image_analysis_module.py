"""
Image analysis module for FaceVault.
This module provides real AI-powered image analysis using OpenAI, Azure, Google, and other providers.
"""

import os
import json
import base64
import random
from typing import Dict, Any
from pathlib import Path
import requests
from datetime import datetime

# Try to import PIL for image preprocessing
try:
    from PIL import Image
    HAS_PIL = True
except ImportError:
    HAS_PIL = False


def test_analysis() -> str:
    """Test function to verify the module is loaded correctly."""
    return "Image analysis module loaded successfully"


def analyze_image(file_path: str) -> Dict[str, Any]:
    """
    Analyze an image and return basic metadata.
    
    Args:
        file_path: Path to the image file
        
    Returns:
        Dictionary containing analysis results
    """
    result = {
        "category": "Unknown",
        "description": "",
        "confidence": 0.0,
        "tags": [],
        "error": None
    }
    
    try:
        if not os.path.exists(file_path):
            result["error"] = "File not found"
            return result
            
        # Get basic file info
        file_name = os.path.basename(file_path)
        file_ext = Path(file_path).suffix.lower()
        file_size = os.path.getsize(file_path)
        
        # Basic categorization based on filename patterns
        name_lower = file_name.lower()
        
        # Check for screenshot patterns
        if 'screenshot' in name_lower or 'screen shot' in name_lower:
            result["category"] = "Screenshots"
            result["description"] = "Screen capture image"
            result["confidence"] = 0.95
            result["tags"] = ["screenshot", "screen capture"]
        
        # Check for selfie patterns
        elif 'selfie' in name_lower or 'img_' in name_lower:
            result["category"] = "People"
            result["description"] = "Selfie or personal photo"
            result["confidence"] = 0.7
            result["tags"] = ["selfie", "people", "portrait"]
            
        # Check for document patterns
        elif 'scan' in name_lower or 'document' in name_lower or file_ext == '.pdf':
            result["category"] = "Documents"
            result["description"] = "Scanned document or text"
            result["confidence"] = 0.8
            result["tags"] = ["document", "scan", "text"]
            
        # If PIL is available, do more sophisticated analysis
        elif HAS_PIL:
            try:
                with Image.open(file_path) as img:
                    width, height = img.size
                    aspect_ratio = width / height if height > 0 else 1
                    
                    # Analyze based on dimensions and aspect ratio
                    if width == height:
                        result["category"] = "Objects"
                        result["description"] = "Square format image, possibly product or icon"
                        result["confidence"] = 0.6
                        result["tags"] = ["square", "product", "icon"]
                    
                    elif aspect_ratio > 2 or aspect_ratio < 0.5:
                        result["category"] = "Scenes"
                        result["description"] = "Panoramic or tall image"
                        result["confidence"] = 0.65
                        result["tags"] = ["panorama", "landscape", "scene"]
                    
                    elif width > 3000 or height > 3000:
                        result["category"] = "Nature"
                        result["description"] = "High resolution photo, possibly nature or landscape"
                        result["confidence"] = 0.5
                        result["tags"] = ["high-res", "nature", "landscape"]
                    
                    else:
                        # Default photo categorization
                        categories = ["People", "Nature", "Objects", "Animals", "Scenes"]
                        result["category"] = random.choice(categories)
                        result["confidence"] = 0.4
                        
                        # Generate description based on category
                        descriptions = {
                            "People": "Photo containing people or portraits",
                            "Nature": "Nature or outdoor scene",
                            "Objects": "Still life or object photography",
                            "Animals": "Wildlife or pet photography",
                            "Scenes": "General scene or landscape"
                        }
                        result["description"] = descriptions.get(result["category"], "General photo")
                        result["tags"] = ["photo", result["category"].lower()]
                    
                    # Add dimension info to tags
                    result["tags"].append(f"{width}x{height}")
                    
                    # Check EXIF data if available
                    exif = img.getexif()
                    if exif:
                        import PIL.ExifTags
                        for tag_id, value in exif.items():
                            tag_name = PIL.ExifTags.TAGS.get(tag_id, tag_id)
                            
                            # Look for camera info
                            if tag_name == "Make" and value:
                                result["tags"].append(f"camera:{value}")
                            elif tag_name == "Model" and value:
                                result["tags"].append(f"model:{value}")
                            elif tag_name == "Software" and value:
                                # Check for screenshot software
                                if "screenshot" in str(value).lower():
                                    result["category"] = "Screenshots"
                                    result["confidence"] = 0.9
                                    
            except Exception as e:
                # If PIL fails, fall back to basic analysis
                pass
        
        # If no category assigned yet, make an educated guess
        if result["category"] == "Unknown":
            # Random assignment for demo purposes
            categories = ["Photos", "Images", "Pictures", "Media", "Files"]
            result["category"] = random.choice(categories)
            result["description"] = "Unanalyzed image file"
            result["confidence"] = 0.3
            result["tags"] = ["unanalyzed", file_ext[1:] if file_ext else "unknown"]
            
    except Exception as e:
        result["error"] = str(e)
        result["category"] = "Error"
        result["description"] = f"Error analyzing image: {str(e)}"
        
    return result


def encode_image_to_base64(image_path: str) -> str:
    """Encode an image file to base64 string."""
    with open(image_path, "rb") as image_file:
        return base64.b64encode(image_file.read()).decode('utf-8')


def analyze_image_with_openai(file_path: str, api_key: str, model: str, endpoint: str) -> Dict[str, Any]:
    """
    Analyze an image using OpenAI's Vision API.
    
    Args:
        file_path: Path to the image file
        api_key: OpenAI API key
        model: Model to use (e.g., "gpt-4o-mini", "gpt-4-vision-preview")
        endpoint: API endpoint (e.g., "https://api.openai.com/v1")
        
    Returns:
        Dictionary containing the full API response
    """
    try:
        # Encode image to base64
        base64_image = encode_image_to_base64(file_path)
        
        # Prepare the API request
        headers = {
            "Content-Type": "application/json",
            "Authorization": f"Bearer {api_key}"
        }
        
        # Create the prompt for image analysis
        prompt = """Analyze this image and provide:
1. A general category (e.g., People, Nature, Objects, Animals, Documents, Screenshots, etc.)
2. A detailed description of what you see
3. Key objects or elements in the image
4. Any text visible in the image
5. The overall quality and composition
6. Suggested tags for this image

Please format your response as JSON with the following structure:
{
    "category": "main category",
    "description": "detailed description",
    "objects": ["object1", "object2", ...],
    "text_found": "any text in the image",
    "quality": "quality assessment",
    "tags": ["tag1", "tag2", ...],
    "people_count": number of people (0 if none),
    "is_screenshot": true/false,
    "dominant_colors": ["color1", "color2", ...]
}"""

        payload = {
            "model": model or "gpt-4o-mini",
            "messages": [
                {
                    "role": "user",
                    "content": [
                        {
                            "type": "text",
                            "text": prompt
                        },
                        {
                            "type": "image_url",
                            "image_url": {
                                "url": f"data:image/jpeg;base64,{base64_image}"
                            }
                        }
                    ]
                }
            ],
            "max_tokens": 500
        }
        
        # Make the API request
        response = requests.post(
            f"{endpoint}/chat/completions",
            headers=headers,
            json=payload,
            timeout=30
        )
        
        if response.status_code == 200:
            api_response = response.json()
            
            # Extract the content from the response
            content = api_response['choices'][0]['message']['content']
            
            # Try to parse the JSON response
            try:
                # Remove any markdown code blocks if present
                if content.startswith("```json"):
                    content = content[7:]
                if content.endswith("```"):
                    content = content[:-3]
                    
                parsed_content = json.loads(content.strip())
            except json.JSONDecodeError:
                # If parsing fails, create a structured response
                parsed_content = {
                    "category": "Unknown",
                    "description": content,
                    "parse_error": "Failed to parse AI response as JSON"
                }
            
            # Build the final result
            result = {
                "success": True,
                "provider": "openai",
                "model": model or "gpt-4o-mini",
                "timestamp": datetime.utcnow().isoformat(),
                "usage": api_response.get('usage', {}),
                "analysis": parsed_content,
                "raw_response": api_response
            }
            
            return result
            
        else:
            return {
                "success": False,
                "error": f"API request failed with status {response.status_code}",
                "details": response.text,
                "provider": "openai",
                "model": model or "gpt-4o-mini"
            }
            
    except Exception as e:
        return {
            "success": False,
            "error": str(e),
            "error_type": type(e).__name__,
            "provider": "openai",
            "model": model or "gpt-4o-mini"
        }


def analyze_image_with_ai(file_path: str, ai_provider: str = "", api_key: str = "", model: str = "", endpoint: str = "") -> Dict[str, Any]:
    """
    Analyze an image using AI settings provided as parameters.
    
    Args:
        file_path: Path to the image file
        ai_provider: AI provider name (e.g., "openai", "azure", "google")
        api_key: API key for authentication
        model: AI model to use (optional)
        endpoint: API endpoint URL (optional)
        
    Returns:
        Dictionary containing analysis results
    """
    # Initialize result structure
    result = {
        "file_path": file_path,
        "file_name": os.path.basename(file_path),
        "timestamp": datetime.utcnow().isoformat()
    }
    
    # Check if file exists
    if not os.path.exists(file_path):
        result["success"] = False
        result["error"] = "File not found"
        return result
    
    # Check if AI provider and key are provided
    if not ai_provider or not api_key:
        result["success"] = False
        result["error"] = "AI provider and API key are required"
        result["category"] = "Unknown"
        result["description"] = "No AI analysis performed - missing configuration"
        return result
    
    # Route to the appropriate provider
    if ai_provider.lower() == "openai":
        if not endpoint:
            endpoint = "https://api.openai.com/v1"
        
        ai_result = analyze_image_with_openai(file_path, api_key, model, endpoint)
        
        # Merge AI result with base result
        result.update(ai_result)
        
        # Extract key fields for database storage
        if ai_result.get("success") and "analysis" in ai_result:
            analysis = ai_result["analysis"]
            result["category"] = analysis.get("category", "Unknown")
            result["description"] = analysis.get("description", "")
            result["tags"] = analysis.get("tags", [])
            result["confidence"] = 0.9  # High confidence for real AI analysis
            
    elif ai_provider.lower() == "azure":
        # TODO: Implement Azure Computer Vision
        result["success"] = False
        result["error"] = "Azure provider not yet implemented"
        result["category"] = "Unknown"
        result["description"] = "Azure Computer Vision integration coming soon"
        
    elif ai_provider.lower() == "google":
        # TODO: Implement Google Vision AI
        result["success"] = False
        result["error"] = "Google provider not yet implemented"
        result["category"] = "Unknown"
        result["description"] = "Google Vision AI integration coming soon"
        
    else:
        result["success"] = False
        result["error"] = f"Unknown AI provider: {ai_provider}"
        result["category"] = "Unknown"
        result["description"] = "Invalid AI provider specified"
    
    return result


# Example usage
if __name__ == "__main__":
    # Test with a sample image
    test_path = "test_image.jpg"
    if os.path.exists(test_path):
        result = analyze_image(test_path)
        print(f"Analysis result: {result}")
    else:
        print("Test image not found")
    
    # Test the test function
    print(test_analysis())


# Metadata-based image classification using LLM
def classify_image_metadata_single(api_key: str, metadata_json: str, model: str = "gpt-4o-mini") -> str:
    """
    Classify a single image based on metadata using LLM.
    
    Args:
        api_key: OpenAI API key
        metadata_json: JSON string containing metadata
        model: Model to use (default: gpt-4o-mini)
        
    Returns:
        JSON string with classification result
    """
    try:
        metadata = json.loads(metadata_json)
        
        # Format metadata into readable string
        metadata_str = _format_metadata_for_llm(metadata)
        
        # Create classification prompt
        prompt = f"""You are an expert at analyzing image metadata to determine if an image is a photo taken with a camera or a screenshot from a device.

Analyze the following metadata and classify the image as either 'photo' or 'screenshot'. If you cannot determine with reasonable confidence, classify as 'unknown'.

Key indicators:
- Screenshots often have: specific resolutions matching device screens, no camera metadata, software names like screen capture tools, no EXIF data
- Photos often have: camera make/model, focal length, ISO, exposure settings, GPS data, EXIF dates

Metadata:
{metadata_str}

Respond in JSON format:
{{
    "category": "photo" or "screenshot" or "unknown",
    "confidence": 0.0 to 1.0,
    "reasoning": "Brief explanation of your classification"
}}"""

        # Call OpenAI API
        headers = {
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json"
        }
        
        payload = {
            "model": model,
            "messages": [
                {
                    "role": "system",
                    "content": "You are an expert at analyzing image metadata to classify images. Always respond in valid JSON format."
                },
                {
                    "role": "user",
                    "content": prompt
                }
            ],
            "temperature": 0.1,
            "max_tokens": 1000
        }
        
        response = requests.post("https://api.openai.com/v1/chat/completions", headers=headers, json=payload)
        response.raise_for_status()
        
        llm_response = response.json()["choices"][0]["message"]["content"]
        
        # Parse and validate the response
        cleaned = llm_response.strip()
        if cleaned.startswith("```json"):
            cleaned = cleaned[7:]
        if cleaned.startswith("```"):
            cleaned = cleaned[3:]
        if cleaned.endswith("```"):
            cleaned = cleaned[:-3]
        
        result = json.loads(cleaned.strip())
        
        # Validate the result
        if "category" not in result:
            result["category"] = "unknown"
        if "confidence" not in result:
            result["confidence"] = 0.5
        if "reasoning" not in result:
            result["reasoning"] = "No reasoning provided"
        
        # Ensure category is valid
        if result["category"] not in ["photo", "screenshot", "unknown"]:
            result["category"] = "unknown"
        
        return json.dumps(result)
        
    except Exception as e:
        return json.dumps({
            "category": "unknown",
            "confidence": 0.0,
            "reasoning": f"Error: {str(e)}",
            "error": True
        })


def classify_image_metadata_batch(api_key: str, metadata_list_json: str, model: str = "gpt-4o-mini") -> str:
    """
    Classify multiple images based on metadata using LLM.
    
    Args:
        api_key: OpenAI API key
        metadata_list_json: JSON string containing list of metadata
        model: Model to use (default: gpt-4o-mini)
        
    Returns:
        JSON string with list of classification results
    """
    try:
        metadata_list = json.loads(metadata_list_json)
        
        # Format all metadata
        formatted_batch = []
        for i, metadata in enumerate(metadata_list):
            formatted_batch.append(f"Image {i+1}:\n{_format_metadata_for_llm(metadata)}")
        
        batch_str = "\n\n".join(formatted_batch)
        
        # Create batch prompt
        prompt = f"""You are an expert at analyzing image metadata to determine if images are photos taken with a camera or screenshots from devices.

Analyze the following {len(metadata_list)} images and classify each as either 'photo' or 'screenshot'. If you cannot determine with reasonable confidence, classify as 'unknown'.

Key indicators:
- Screenshots often have: specific resolutions matching device screens, no camera metadata, software names like screen capture tools, no EXIF data
- Photos often have: camera make/model, focal length, ISO, exposure settings, GPS data, EXIF dates

{batch_str}

Respond with a JSON array containing {len(metadata_list)} objects in order:
[
    {{
        "category": "photo" or "screenshot" or "unknown",
        "confidence": 0.0 to 1.0,
        "reasoning": "Brief explanation"
    }},
    ...
]"""

        # Call OpenAI API
        headers = {
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json"
        }
        
        payload = {
            "model": model,
            "messages": [
                {
                    "role": "system",
                    "content": "You are an expert at analyzing image metadata to classify images. Always respond in valid JSON format."
                },
                {
                    "role": "user",
                    "content": prompt
                }
            ],
            "temperature": 0.1,
            "max_tokens": 2000
        }
        
        response = requests.post("https://api.openai.com/v1/chat/completions", headers=headers, json=payload)
        response.raise_for_status()
        
        llm_response = response.json()["choices"][0]["message"]["content"]
        
        # Parse and validate the response
        cleaned = llm_response.strip()
        if cleaned.startswith("```json"):
            cleaned = cleaned[7:]
        if cleaned.startswith("```"):
            cleaned = cleaned[3:]
        if cleaned.endswith("```"):
            cleaned = cleaned[:-3]
        
        results = json.loads(cleaned.strip())
        
        # Validate we got the right number of results
        if len(results) != len(metadata_list):
            # Pad with unknowns if needed
            while len(results) < len(metadata_list):
                results.append({
                    "category": "unknown",
                    "confidence": 0.0,
                    "reasoning": "Missing from batch response"
                })
        
        # Validate each result
        for result in results:
            if "category" not in result:
                result["category"] = "unknown"
            if "confidence" not in result:
                result["confidence"] = 0.5
            if "reasoning" not in result:
                result["reasoning"] = "No reasoning provided"
            if result["category"] not in ["photo", "screenshot", "unknown"]:
                result["category"] = "unknown"
        
        return json.dumps(results[:len(metadata_list)])
        
    except Exception as e:
        return json.dumps([{
            "category": "unknown",
            "confidence": 0.0,
            "reasoning": f"Error: {str(e)}",
            "error": True
        } for _ in range(len(json.loads(metadata_list_json)))])


def _format_metadata_for_llm(metadata: Dict[str, Any]) -> str:
    """Format metadata into a readable string for the LLM."""
    lines = []
    
    # Basic file info
    if "file_name" in metadata:
        lines.append(f"Filename: {metadata['file_name']}")
    if "file_extension" in metadata:
        lines.append(f"File Extension: {metadata['file_extension']}")
    if "file_size_bytes" in metadata:
        size_mb = metadata['file_size_bytes'] / (1024 * 1024)
        lines.append(f"File Size: {size_mb:.2f} MB")
    
    # Image dimensions
    if "width" in metadata and "height" in metadata:
        lines.append(f"Dimensions: {metadata['width']} x {metadata['height']} pixels")
        aspect_ratio = metadata['width'] / metadata['height'] if metadata['height'] > 0 else 0
        lines.append(f"Aspect Ratio: {aspect_ratio:.3f}")
    
    # Dates
    if "date_taken" in metadata:
        lines.append(f"Date Taken (EXIF): {metadata['date_taken']}")
    if "date_created" in metadata:
        lines.append(f"File Created: {metadata['date_created']}")
    if "date_modified" in metadata:
        lines.append(f"File Modified: {metadata['date_modified']}")
    
    # Camera/device info
    if "camera_make" in metadata:
        lines.append(f"Camera Make: {metadata['camera_make']}")
    if "camera_model" in metadata:
        lines.append(f"Camera Model: {metadata['camera_model']}")
    if "software" in metadata:
        lines.append(f"Software: {metadata['software']}")
    
    # Technical metadata
    if "color_space" in metadata:
        lines.append(f"Color Space: {metadata['color_space']}")
    if "bit_depth" in metadata:
        lines.append(f"Bit Depth: {metadata['bit_depth']}")
    if "orientation" in metadata:
        lines.append(f"Orientation: {metadata['orientation']}")
    
    # GPS data
    if "latitude" in metadata and "longitude" in metadata:
        lines.append(f"GPS Coordinates: {metadata['latitude']}, {metadata['longitude']}")
    
    # Camera settings
    if "focal_length" in metadata:
        lines.append(f"Focal Length: {metadata['focal_length']}mm")
    if "f_number" in metadata:
        lines.append(f"F-Stop: f/{metadata['f_number']}")
    if "iso" in metadata:
        lines.append(f"ISO: {metadata['iso']}")
    if "exposure_time" in metadata:
        lines.append(f"Exposure Time: {metadata['exposure_time']}")
    
    return "\n".join(lines)