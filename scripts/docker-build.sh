#!/bin/bash

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${YELLOW}Building Belgium VAT Checker Docker image...${NC}"

# Build the Docker image
if docker build -t belgium-vat-checker:latest .; then
    echo -e "${GREEN}✓ Docker image built successfully!${NC}"
    echo -e "${GREEN}Image name: belgium-vat-checker:latest${NC}"
    
    # Display image size
    echo -e "\n${YELLOW}Image details:${NC}"
    docker images belgium-vat-checker:latest
else
    echo -e "${RED}✗ Docker build failed!${NC}"
    exit 1
fi