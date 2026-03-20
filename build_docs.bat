@echo off
echo [DOCS] Building Host Link .NET Docs with DocFX...

:: 1. Generate Metadata from source
docfx metadata docfx.json

:: 2. Build the site
docfx build docfx.json

if %errorlevel% neq 0 (
    echo [ERROR] DocFX build failed.
)

echo [SUCCESS] Documentation built to docs/

