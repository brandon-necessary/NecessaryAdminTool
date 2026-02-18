$outDir = 'C:\Users\brandon.necessary\source\repos\NecessaryAdminTool\Installer\Dependencies'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory $outDir | Out-Null }

$url = 'https://go.microsoft.com/fwlink/?LinkId=2085155'
$out = Join-Path $outDir 'ndp48-web.exe'

if (-not (Test-Path $out)) {
    Write-Host "Downloading .NET 4.8 web installer from Microsoft..."
    (New-Object System.Net.WebClient).DownloadFile($url, $out)
}
$size = [math]::Round((Get-Item $out).Length / 1KB, 0)
Write-Host "Ready: $out ($size KB)"
