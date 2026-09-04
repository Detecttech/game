# ==============================================================================
# Package QuizBattle for Cloud Deployment (Creates lightweight zip)
# ==============================================================================

$outputZip = "d:\game\quizbattle-cloud.zip"
$tempDir = "d:\game\.cloud-package-temp"

Write-Host "Ensuring server & web portal are freshly compiled..." -ForegroundColor Cyan
Push-Location "d:\game\server"
& npm.cmd run build
Push-Location "d:\game\server\web-portal"
& npm.cmd run build
Pop-Location
Pop-Location

if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }
if (Test-Path $outputZip) { Remove-Item -Force $outputZip }

New-Item -ItemType Directory -Path $tempDir | Out-Null

# 1. Copy Docker & Cloud Build files
Copy-Item "d:\game\Dockerfile" "$tempDir\"
Copy-Item "d:\game\.dockerignore" "$tempDir\"
Copy-Item "d:\game\.gcloudignore" "$tempDir\"
Copy-Item "d:\game\cloudbuild.yaml" "$tempDir\"

# 2. Copy deploy scripts
Copy-Item -Recurse "d:\game\deploy" "$tempDir\deploy"

# 3. Copy shared assets
Copy-Item -Recurse "d:\game\shared" "$tempDir\shared"

# 4. Copy game-client/webgl-build (pre-compiled game, omitting debug-only Burst info)
New-Item -ItemType Directory -Path "$tempDir\game-client\webgl-build" | Out-Null
if (Test-Path "d:\game\game-client\webgl-build") {
    Get-ChildItem "d:\game\game-client\webgl-build" -Exclude "*BurstDebugInformation*" | Copy-Item -Destination "$tempDir\game-client\webgl-build" -Recurse
}

# 5. Copy pre-compiled server artifacts and package specs
New-Item -ItemType Directory -Path "$tempDir\server" | Out-Null
Copy-Item -Recurse "d:\game\server\dist" "$tempDir\server\dist"
Copy-Item -Recurse "d:\game\server\src" "$tempDir\server\src"
Copy-Item "d:\game\server\package.json" "$tempDir\server\"
Copy-Item "d:\game\server\package-lock.json" "$tempDir\server\"
Copy-Item "d:\game\server\tsconfig.json" "$tempDir\server\"

# 6. Copy pre-compiled web portal
New-Item -ItemType Directory -Path "$tempDir\server\web-portal" | Out-Null
Copy-Item -Recurse "d:\game\server\web-portal\dist" "$tempDir\server\web-portal\dist"
Copy-Item "d:\game\server\web-portal\package.json" "$tempDir\server\web-portal\"
Copy-Item "d:\game\server\web-portal\package-lock.json" "$tempDir\server\web-portal\"

# Create the zip archive
Compress-Archive -Path "$tempDir\*" -DestinationPath $outputZip -CompressionLevel Optimal

# Cleanup temp folder
Remove-Item -Recurse -Force $tempDir

$zipSize = (Get-Item $outputZip).Length / 1MB
Write-Host "`nPackage created successfully!" -ForegroundColor Green
Write-Host "File: $outputZip" -ForegroundColor Yellow
Write-Host "Size: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Green
Write-Host "Ready to upload to Google Cloud Shell in seconds!`n" -ForegroundColor Cyan
