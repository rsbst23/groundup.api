#!/bin/sh
set -e

echo "Starting GroundUp API..."

# Run the application
exec dotnet GroundUp.api.dll
