#!/bin/bash
set -e

echo "=== Tika.BatchIngestor Build and Test Script ==="
echo ""

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Check .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}ERROR: .NET SDK not found. Please install .NET 8 SDK${NC}"
    echo "Download from: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
fi

echo -e "${GREEN}✓${NC} .NET SDK found: $(dotnet --version)"
echo ""

# Restore
echo "Step 1: Restoring NuGet packages..."
dotnet restore
echo -e "${GREEN}✓${NC} Packages restored"
echo ""

# Build
echo "Step 2: Building solution (Release)..."
dotnet build --configuration Release --no-restore
echo -e "${GREEN}✓${NC} Build successful"
echo ""

# Test
echo "Step 3: Running tests..."
dotnet test --configuration Release --no-build --verbosity normal
echo -e "${GREEN}✓${NC} All tests passed"
echo ""

# Optional: Run sample
read -p "Run sample application? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "Running sample..."
    cd src/Tika.BatchIngestor.Samples
    dotnet run --configuration Release --no-build
    cd ../..
fi

echo ""
echo -e "${GREEN}=== Build Complete ===${NC}"
echo "Next steps:"
echo "1. Review DEPLOYMENT_GUIDE.md for GitHub/NuGet setup"
echo "2. Initialize git and push to GitHub"
echo "3. Set up NuGet API key and publish"
