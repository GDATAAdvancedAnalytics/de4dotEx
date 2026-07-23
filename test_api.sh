#!/bin/bash

# Exit on error
set -e

# Help / Usage check
if [ $# -lt 1 ]; then
    echo "=========================================================================="
    echo " de4dotEx HTTP Web API Test Client"
    echo "=========================================================================="
    echo "Usage: $0 <file_to_deobfuscate> [optional_extra_flags]"
    echo ""
    echo "Examples:"
    echo "  $0 MyProtectedApp.dll"
    echo "  $0 /path/to/Target.exe \"--preserve-tokens -str delegate\""
    echo "=========================================================================="
    exit 1
fi

INPUT_FILE="$1"
EXTRA_OPTIONS="$2"

# Verify input file exists
if [ ! -f "$INPUT_FILE" ]; then
    echo "Error: File '$INPUT_FILE' not found."
    exit 1
fi

# Calculate directory and output filename (appending _cleaned before the extension)
DIR=$(dirname "$INPUT_FILE")
FILENAME=$(basename "$INPUT_FILE")
EXTENSION="${FILENAME##*.}"
BASENAME="${FILENAME%.*}"

# Handle files with no extensions
if [ "$BASENAME" = "$EXTENSION" ]; then
    OUTPUT_FILE="${DIR}/${BASENAME}_cleaned"
else
    OUTPUT_FILE="${DIR}/${BASENAME}_cleaned.${EXTENSION}"
fi

echo "[de4dotEx] Uploading '$INPUT_FILE' to http://localhost:8080/deobfuscate..."
if [ -n "$EXTRA_OPTIONS" ]; then
    echo "[de4dotEx] Applying native flags: $EXTRA_OPTIONS"
fi

# Perform HTTP POST request and capture HTTP status code
HTTP_CODE=$(curl -s -w "%{http_code}" \
    -F "file=@${INPUT_FILE}" \
    -F "options=${EXTRA_OPTIONS}" \
    http://localhost:8080/deobfuscate \
    -o "${OUTPUT_FILE}")

if [ "$HTTP_CODE" -eq 200 ]; then
    echo "------------------------------------------------------------"
    echo "🎉 Success! Cleaned assembly saved to:"
    echo "   $OUTPUT_FILE"
    echo "------------------------------------------------------------"
else
    echo "------------------------------------------------------------"
    echo "❌ Error: Deobfuscation failed (HTTP status: ${HTTP_CODE})."
    
    # If server returned an error payload, display it and clean up the file
    if [ -f "${OUTPUT_FILE}" ]; then
        echo "Server message:"
        cat "${OUTPUT_FILE}"
        echo ""
        rm -f "${OUTPUT_FILE}"
    fi
    echo "------------------------------------------------------------"
    exit 1
fi
