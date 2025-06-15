#!/bin/bash

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${YELLOW}Starting Belgium VAT Checker API...${NC}"

# Run the Docker container
docker run -d \
    --name belgium-vat-checker \
    -p 5000:8080 \
    -p 5001:8081 \
    -e ASPNETCORE_ENVIRONMENT=Development \
    --restart unless-stopped \
    belgium-vat-checker:latest

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Container started successfully!${NC}"
    echo -e "\n${BLUE}API endpoints:${NC}"
    echo -e "  HTTP:    http://localhost:5000"
    echo -e "  HTTPS:   https://localhost:5001"
    echo -e "  Swagger: http://localhost:5000/swagger"
    echo -e "\n${YELLOW}Container commands:${NC}"
    echo -e "  View logs:    docker logs -f belgium-vat-checker"
    echo -e "  Stop:         docker stop belgium-vat-checker"
    echo -e "  Start:        docker start belgium-vat-checker"
    echo -e "  Remove:       docker rm -f belgium-vat-checker"
    echo -e "  Status:       docker ps | grep belgium-vat-checker"
fi