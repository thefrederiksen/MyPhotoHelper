"""
Metadata-based image classification using LLM.
Analyzes image metadata to classify as photo, screenshot, or unknown.
"""

import json
import logging
from typing import Dict, Any, List, Optional
import requests
from datetime import datetime

logger = logging.getLogger(__name__)

class MetadataClassifier:
    """Classifies images as photo/screenshot based on metadata analysis using LLM."""
    
    def __init__(self, api_key: str, api_endpoint: str = "https://api.openai.com/v1/chat/completions", model: str = "gpt-4o-mini"):
        """Initialize the classifier with OpenAI API credentials."""
        self.api_key = api_key
        self.api_endpoint = api_endpoint
        self.model = model
        self.headers = {
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json"
        }
    
    def classify_image_metadata(self, metadata: Dict[str, Any]) -> Dict[str, Any]:
        """
        Classify an image based on its metadata.
        
        Args:
            metadata: Dictionary containing all available metadata
            
        Returns:
            Dictionary with classification result and confidence
        """
        try:
            # Prepare the metadata for analysis
            metadata_str = self._format_metadata(metadata)
            
            # Create the prompt
            prompt = self._create_classification_prompt(metadata_str)
            
            # Call the LLM
            response = self._call_llm(prompt)
            
            # Parse the response
            result = self._parse_llm_response(response)
            
            return result
            
        except Exception as e:
            logger.error(f"Error classifying metadata: {str(e)}")
            return {
                "category": "unknown",
                "confidence": 0.0,
                "reasoning": f"Error: {str(e)}",
                "error": True
            }
    
    def classify_batch(self, metadata_list: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """
        Classify multiple images in a single LLM call for efficiency.
        
        Args:
            metadata_list: List of metadata dictionaries
            
        Returns:
            List of classification results
        """
        try:
            # Format all metadata
            formatted_batch = []
            for i, metadata in enumerate(metadata_list):
                formatted_batch.append(f"Image {i+1}:\n{self._format_metadata(metadata)}")
            
            batch_str = "\n\n".join(formatted_batch)
            
            # Create batch prompt
            prompt = self._create_batch_classification_prompt(batch_str, len(metadata_list))
            
            # Call the LLM
            response = self._call_llm(prompt)
            
            # Parse batch response
            results = self._parse_batch_response(response, len(metadata_list))
            
            return results
            
        except Exception as e:
            logger.error(f"Error in batch classification: {str(e)}")
            # Return unknown for all images in case of error
            return [{"category": "unknown", "confidence": 0.0, "error": True} for _ in metadata_list]
    
    def _format_metadata(self, metadata: Dict[str, Any]) -> str:
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
    
    def _create_classification_prompt(self, metadata_str: str) -> str:
        """Create the prompt for single image classification."""
        return f"""You are an expert at analyzing image metadata to determine if an image is a photo taken with a camera or a screenshot from a device.

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

    def _create_batch_classification_prompt(self, batch_str: str, count: int) -> str:
        """Create the prompt for batch classification."""
        return f"""You are an expert at analyzing image metadata to determine if images are photos taken with a camera or screenshots from devices.

Analyze the following {count} images and classify each as either 'photo' or 'screenshot'. If you cannot determine with reasonable confidence, classify as 'unknown'.

Key indicators:
- Screenshots often have: specific resolutions matching device screens, no camera metadata, software names like screen capture tools, no EXIF data
- Photos often have: camera make/model, focal length, ISO, exposure settings, GPS data, EXIF dates

{batch_str}

Respond with a JSON array containing {count} objects in order:
[
    {{
        "category": "photo" or "screenshot" or "unknown",
        "confidence": 0.0 to 1.0,
        "reasoning": "Brief explanation"
    }},
    ...
]"""
    
    def _call_llm(self, prompt: str) -> str:
        """Make the API call to the LLM."""
        payload = {
            "model": self.model,
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
            "temperature": 0.1,  # Low temperature for more consistent classification
            "max_tokens": 1000
        }
        
        response = requests.post(self.api_endpoint, headers=self.headers, json=payload)
        response.raise_for_status()
        
        return response.json()["choices"][0]["message"]["content"]
    
    def _parse_llm_response(self, response: str) -> Dict[str, Any]:
        """Parse the LLM response into a structured result."""
        try:
            # Clean up the response (remove markdown code blocks if present)
            cleaned = response.strip()
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
            
            return result
            
        except json.JSONDecodeError as e:
            logger.error(f"Failed to parse LLM response: {response}")
            return {
                "category": "unknown",
                "confidence": 0.0,
                "reasoning": f"Failed to parse response: {str(e)}",
                "error": True
            }
    
    def _parse_batch_response(self, response: str, expected_count: int) -> List[Dict[str, Any]]:
        """Parse the batch LLM response."""
        try:
            # Clean up the response
            cleaned = response.strip()
            if cleaned.startswith("```json"):
                cleaned = cleaned[7:]
            if cleaned.startswith("```"):
                cleaned = cleaned[3:]
            if cleaned.endswith("```"):
                cleaned = cleaned[:-3]
            
            results = json.loads(cleaned.strip())
            
            # Validate we got the right number of results
            if len(results) != expected_count:
                logger.warning(f"Expected {expected_count} results but got {len(results)}")
                # Pad with unknowns if needed
                while len(results) < expected_count:
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
            
            return results[:expected_count]  # Ensure we don't return more than expected
            
        except Exception as e:
            logger.error(f"Failed to parse batch response: {str(e)}")
            return [{"category": "unknown", "confidence": 0.0, "error": True} for _ in range(expected_count)]


# Module functions for CSnakes integration
def classify_single(api_key: str, metadata_json: str, model: str = "gpt-4o-mini") -> str:
    """
    Classify a single image based on metadata.
    
    Args:
        api_key: OpenAI API key
        metadata_json: JSON string containing metadata
        model: Model to use (default: gpt-4o-mini)
        
    Returns:
        JSON string with classification result
    """
    try:
        metadata = json.loads(metadata_json)
        classifier = MetadataClassifier(api_key, model=model)
        result = classifier.classify_image_metadata(metadata)
        return json.dumps(result)
    except Exception as e:
        return json.dumps({
            "category": "unknown",
            "confidence": 0.0,
            "reasoning": str(e),
            "error": True
        })


def classify_batch(api_key: str, metadata_list_json: str, model: str = "gpt-4o-mini") -> str:
    """
    Classify multiple images based on metadata.
    
    Args:
        api_key: OpenAI API key
        metadata_list_json: JSON string containing list of metadata
        model: Model to use (default: gpt-4o-mini)
        
    Returns:
        JSON string with list of classification results
    """
    try:
        metadata_list = json.loads(metadata_list_json)
        classifier = MetadataClassifier(api_key, model=model)
        results = classifier.classify_batch(metadata_list)
        return json.dumps(results)
    except Exception as e:
        return json.dumps([{
            "category": "unknown",
            "confidence": 0.0,
            "reasoning": str(e),
            "error": True
        }])