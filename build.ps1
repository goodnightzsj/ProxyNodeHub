# ProxyNodeHub 一键编译 + 打包脚本
# 用法: 在项目根目录运行  powershell -ExecutionPolicy Bypass -File build.ps1
# 产物: dist/self-contained/ 与 dist/framework-dependent/，压缩包输出到 Releases/

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root "src"
$dist = Join-Path $root "dist"
$rels = Join-Path $root "Releases"

# 版本号从 csproj 读取，避免两处不同步
$csproj = Join-Path $src "ProxyNodeHub.csproj"
$m = Select-String -Path $csproj -Pattern '<Version>(.*?)</Version>'
$ver = if ($m) { $m.Matches[0].Groups[1].Value } else { "0.0.1" }
Write-Host "== ProxyNodeHub v$ver 编译 ==" -ForegroundColor Cyan

# 清理旧产物
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force -Path $dist | Out-Null

# 1) 自包含版（单文件，无需 .NET 运行时）
Write-Host "  [1/2] 自包含版…" -ForegroundColor Yellow
dotnet publish $src -c Release -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -p:DebugSymbols=false `
    -o (Join-Path $dist "self-contained")
if ($LASTEXITCODE -ne 0) { throw "自包含版编译失败" }

# 2) 框架依赖版（需要 .NET 8 桌面运行时）
Write-Host "  [2/2] 框架依赖版…" -ForegroundColor Yellow
dotnet publish $src -c Release -r win-x64 `
    --self-contained false `
    -o (Join-Path $dist "framework-dependent")
if ($LASTEXITCODE -ne 0) { throw "框架依赖版编译失败" }

# 3) 打包 zip
Write-Host "== 打包 zip ==" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $rels | Out-Null
Compress-Archive -Path (Join-Path $dist "self-contained\*") `
    -DestinationPath (Join-Path $rels "ProxyNodeHub_v${ver}_self-contained.zip") `
    -CompressionLevel Optimal -Force
Compress-Archive -Path (Join-Path $dist "framework-dependent\*") `
    -DestinationPath (Join-Path $rels "ProxyNodeHub_v${ver}_framework-dependent.zip") `
    -CompressionLevel Optimal -Force

Write-Host "== 完成 ==" -ForegroundColor Green
Get-ChildItem $rels -Filter "ProxyNodeHub_v${ver}_*.zip" |
    Select-Object Name, @{n='MB'; e={[math]::Round($_.Length/1MB,2)}} |
    Format-Table -AutoSize
