$ErrorActionPreference = 'Stop'

$taskConfig = Get-Content -Raw (Join-Path $PSScriptRoot 'appsettings.json') | ConvertFrom-Json
$env:ConnectionStrings__ManpowerDb = $taskConfig.ConnectionStrings.ManpowerDb

dotnet watch run --no-restore
