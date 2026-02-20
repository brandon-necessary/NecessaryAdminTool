$msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path $msbuild)) {
    $msbuild = Get-ChildItem 'C:\Program Files\Microsoft Visual Studio' -Recurse -Filter 'MSBuild.exe' -ErrorAction SilentlyContinue |
               Where-Object { $_.FullName -match 'Current' } | Select-Object -First 1 -ExpandProperty FullName
}
Write-Host "Using MSBuild: $msbuild"

# Build main NAT application
Write-Host "`n=== Building NecessaryAdminTool ===" -ForegroundColor Cyan
& $msbuild 'C:\Users\brandon.necessary\source\repos\NecessaryAdminTool\NecessaryAdminTool\NecessaryAdminTool.csproj' `
    /p:Configuration=Release /t:Rebuild /v:m
$natExit = $LASTEXITCODE

# Build NecessaryAdminAgent Windows service
Write-Host "`n=== Building NecessaryAdminAgent ===" -ForegroundColor Cyan
& $msbuild 'C:\Users\brandon.necessary\source\repos\NecessaryAdminTool\NecessaryAdminAgent\NecessaryAdminAgent.csproj' `
    /p:Configuration=Release /t:Rebuild /v:m
$agentExit = $LASTEXITCODE

if ($natExit -ne 0) {
    Write-Host "NecessaryAdminTool build FAILED (exit $natExit)" -ForegroundColor Red
    exit $natExit
}
if ($agentExit -ne 0) {
    Write-Host "NecessaryAdminAgent build FAILED (exit $agentExit)" -ForegroundColor Red
    exit $agentExit
}

Write-Host "`nAll builds succeeded." -ForegroundColor Green
exit 0
