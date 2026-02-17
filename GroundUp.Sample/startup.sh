#!/bin/sh
set -e

echo "Starting GroundUp Sample host..."

# Run the host application
exec dotnet GroundUp.Sample.dll
