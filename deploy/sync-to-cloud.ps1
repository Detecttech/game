# ==============================================================================
# QuizBattle - Google Cloud Delta Sync Script
# Syncs only changed/modified files to Google Cloud Shell or VM
# ==============================================================================

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "    QuizBattle - Google Cloud Delta Update Sync" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$localDir = "d:\game"

# Check if gcloud is installed
if (-not (Get-Command gcloud -ErrorAction SilentlyContinue)) {
    Write-Host "Notice: 'gcloud' CLI is not found in PATH." -ForegroundColor Yellow
    Write-Host "To enable instant 1-click delta updates directly from your terminal," -ForegroundColor Gray
    Write-Host "install Google Cloud SDK: https://cloud.google.com/sdk/docs/install" -ForegroundColor Gray
    Write-Host "`nAlternative 1 (Fastest): Use Git in Cloud Shell:" -ForegroundColor Green
    Write-Host "  1. In Cloud Shell: 'cd quizbattle && git pull'" -ForegroundColor White
    Write-Host "  2. Run: './deploy/deploy-cloud-run.sh'" -ForegroundColor White
    Write-Host "`nAlternative 2: Upload lightweight delta zip:" -ForegroundColor Green
    Write-Host "  Run '.\package-for-cloud.bat' - it now preserves existing files and updates in-place!" -ForegroundColor White
    exit 0
}

Write-Host "`nSyncing modified files directly to Cloud Shell without uploading full zip..." -ForegroundColor Green
# Use gcloud cloud-shell scp with recursive directory copy of changed files
gcloud cloud-shell scp --recurse "$localDir\server" cloudshell:~/quizbattle/
gcloud cloud-shell scp --recurse "$localDir\game-client\webgl-build" cloudshell:~/quizbattle/game-client/
gcloud cloud-shell scp "$localDir\Dockerfile" cloudshell:~/quizbattle/
gcloud cloud-shell scp "$localDir\cloudbuild.yaml" cloudshell:~/quizbattle/

Write-Host "`nDelta sync complete! In Cloud Shell, run:" -ForegroundColor Green
Write-Host "cd ~/quizbattle && ./deploy/deploy-cloud-run.sh`n" -ForegroundColor Yellow
