<#
.SYNOPSIS
    不依赖 GitHub Actions 的一键发版：本地构建两种产物 → 创建 GitHub Release → 上传 zip。

.DESCRIPTION
    适用于 Actions 不可用（例如账号账单被锁）但仍想发版的场景。
    只需要一个带 Contents: Read and write 权限的 PAT，无需任何付费功能。

.PARAMETER Version
    版本号，默认从 csproj 的 <Version> 读取。

.PARAMETER Token
    GitHub Personal Access Token。默认读环境变量 GH_TOKEN。

.PARAMETER Repo
    owner/repo，默认从 git remote origin 推断。

.PARAMETER Draft
    创建为草稿，确认后再手动发布。

.PARAMETER SkipBuild
    跳过构建，直接上传 artifacts/release 下已有的 zip。

.EXAMPLE
    $env:GH_TOKEN = "github_pat_xxx"
    .\scripts\release.ps1 -Version 2.1.0
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$Token = $env:GH_TOKEN,
    [string]$Repo,
    [switch]$Draft,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\DeepSeekTray.Avalonia\DeepSeekTray.Avalonia.csproj'
$outDir = Join-Path $repoRoot 'artifacts\release'
$apiVersion = '2022-11-28'

function Write-Step($text) { Write-Host "`n=== $text ===" -ForegroundColor Cyan }
function Write-Ok($text) { Write-Host "  ✓ $text" -ForegroundColor Green }

# ── 版本号 ──────────────────────────────────────────────────────────────
if (-not $Version) {
    $Version = ([xml](Get-Content $project -Raw -Encoding UTF8)).Project.PropertyGroup.Version |
        Where-Object { $_ } | Select-Object -First 1
    if (-not $Version) { throw '无法从 csproj 读取版本号，请用 -Version 指定。' }
}
$Version = $Version.TrimStart('v')
$tag = "v$Version"
Write-Step "版本 $Version（tag: $tag）"

# ── 仓库与凭据 ──────────────────────────────────────────────────────────
if (-not $Repo) {
    $remote = (git -C $repoRoot remote get-url origin 2>$null)
    if ($remote -match 'github\.com[:/](?<owner>[^/]+)/(?<name>[^/.]+)') {
        $Repo = "$($Matches.owner)/$($Matches.name)"
    } else {
        throw '无法推断仓库，请用 -Repo owner/repo 指定。'
    }
}
Write-Ok "仓库：$Repo"

if (-not $Token) {
    throw @'
缺少 Token。请先创建一个 PAT（Settings → Developer settings → Personal access tokens →
Fine-grained tokens，权限选 Contents: Read and write），然后：

    $env:GH_TOKEN = "github_pat_xxx"
    .\scripts\release.ps1
'@
}

$headers = @{
    Authorization          = "Bearer $Token"
    Accept                 = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = $apiVersion
    'User-Agent'           = 'DeepSeekTray-release-script'
}

# ── 构建 ────────────────────────────────────────────────────────────────
$zipFd = Join-Path $outDir "DeepSeekTray-$Version-win-x64-framework-dependent.zip"
$zipSc = Join-Path $outDir "DeepSeekTray-$Version-win-x64-self-contained.zip"

if (-not $SkipBuild) {
    Write-Step '清理输出目录'
    Remove-Item $outDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    Write-Step '发布：框架依赖（单文件，需 .NET 10 运行时）'
    dotnet publish $project -c Release -r win-x64 --no-self-contained `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None -p:DebugSymbols=false -p:Version=$Version `
        -o "$outDir\fd" | Out-Null
    Copy-Item (Join-Path $repoRoot 'LICENSE'),
              (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.md'),
              (Join-Path $repoRoot 'README.md') -Destination "$outDir\fd"
    Compress-Archive -Path "$outDir\fd\*" -DestinationPath $zipFd -Force
    Write-Ok ("{0} ({1:N1} MB)" -f (Split-Path $zipFd -Leaf), ((Get-Item $zipFd).Length / 1MB))

    Write-Step '发布：自包含 + 裁剪（免运行时）'
    try {
        dotnet publish $project -c Release -r win-x64 --self-contained true `
            -p:PublishSingleFile=true -p:PublishTrimmed=true -p:EnableCompressionInSingleFile=true `
            -p:DebugType=None -p:DebugSymbols=false -p:Version=$Version `
            -o "$outDir\sc" | Out-Null
        Copy-Item (Join-Path $repoRoot 'LICENSE'),
                  (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.md'),
                  (Join-Path $repoRoot 'README.md') -Destination "$outDir\sc"
        Compress-Archive -Path "$outDir\sc\*" -DestinationPath $zipSc -Force
        Write-Ok ("{0} ({1:N1} MB)" -f (Split-Path $zipSc -Leaf), ((Get-Item $zipSc).Length / 1MB))
    } catch {
        Write-Warning "自包含构建失败，跳过该附件：$($_.Exception.Message)"
    }
}

$assets = @($zipFd, $zipSc) | Where-Object { Test-Path $_ }
if ($assets.Count -eq 0) { throw "没有可上传的产物，请检查 $outDir" }

# ── Release 正文（取 CHANGELOG 对应段落）────────────────────────────────
$changelog = Join-Path $repoRoot 'CHANGELOG.md'
$body = "Release $Version"
if (Test-Path $changelog) {
    $text = Get-Content $changelog -Raw -Encoding UTF8
    $pattern = "(?ms)^## \[$([regex]::Escape($Version))\][^\r\n]*\r?\n(.*?)(?=^## \[|\z)"
    $match = [regex]::Match($text, $pattern)
    if ($match.Success -and $match.Groups[1].Value.Trim()) {
        $body = $match.Groups[1].Value.Trim()
    }
}

# ── 创建或复用 Release ──────────────────────────────────────────────────
Write-Step "查找 Release $tag"
$release = $null
try {
    $release = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/tags/$tag" -Headers $headers
    Write-Ok "已存在，复用 release id=$($release.id)"
} catch {
    if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw }
}

if (-not $release) {
    Write-Step '创建 Release'
    $payload = @{
        tag_name         = $tag
        name             = "DeepSeek Balance Tray $Version"
        body             = $body
        draft            = [bool]$Draft
        prerelease       = $Version.Contains('-')
        generate_release_notes = $false
    } | ConvertTo-Json

    $release = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases" `
        -Method Post -Headers $headers -Body $payload -ContentType 'application/json'
    Write-Ok "创建成功：$($release.html_url)"
}

# ── 上传附件（同名先删后传）─────────────────────────────────────────────
foreach ($file in $assets) {
    $name = Split-Path $file -Leaf
    $existing = $release.assets | Where-Object { $_.name -eq $name }
    if ($existing) {
        Write-Host "  删除旧附件 $name"
        Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/assets/$($existing.id)" `
            -Method Delete -Headers $headers | Out-Null
    }

    Write-Step "上传 $name"
    $uploadUrl = "https://uploads.github.com/repos/$Repo/releases/$($release.id)/assets?name=$([uri]::EscapeDataString($name))"
    $result = Invoke-RestMethod $uploadUrl -Method Post -Headers $headers `
        -ContentType 'application/zip' -InFile $file
    Write-Ok ("{0}（{1:N1} MB）" -f $result.name, ($result.size / 1MB))
}

Write-Host "`n发布完成：$($release.html_url)" -ForegroundColor Green
