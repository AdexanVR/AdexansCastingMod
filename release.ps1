<#
    Publishes a new version of Adexan's Casting Mod to GitHub Releases.
    Every player's mod checks GitHub on startup and installs the release on its own.

    Usage (from this folder):
        1. Bump PluginInfo.Version in Plugin.cs (e.g. 1.1.0 -> 1.2.0)
        2. Add a "## v1.2.0" section with the changes to CHANGELOG.md
        3. Run:  powershell -ExecutionPolicy Bypass -File .\release.ps1
           Add  -DryRun  to build and checksum without publishing.

    Publishing uses the GitHub CLI (gh) if it is installed and logged in, otherwise the
    GITHUB_TOKEN environment variable (a token with "contents: write" on the repository).
#>
param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# --- version and repository come from the code, so there is a single source of truth
$pluginCs = Get-Content (Join-Path $root 'Plugin.cs') -Raw
if ($pluginCs -notmatch 'Version\s*=\s*"(\d+\.\d+\.\d+)"') { throw 'Could not find PluginInfo.Version in Plugin.cs' }
$version = $Matches[1]
$tag = "v$version"

$modelsCs = Get-Content (Join-Path $root 'Core\Updater\UpdateModels.cs') -Raw
if ($modelsCs -notmatch 'Repository\s*=\s*"([^"/]+/[^"]+)"') { throw 'Could not find UpdateConfig.Repository' }
$repo = $Matches[1]

Write-Host "Releasing $tag to $repo" -ForegroundColor Cyan

# --- release notes from CHANGELOG.md
$notes = "Adexan's Casting Mod $tag"
$changelog = Join-Path $root 'CHANGELOG.md'
if (Test-Path $changelog) {
    $text = Get-Content $changelog -Raw
    $pattern = "(?ms)^##\s*v?" + [regex]::Escape($version) + "\b.*?\r?\n(.*?)(?=^##\s|\z)"
    if ($text -match $pattern) { $notes = $Matches[1].Trim() }
    else { Write-Warning "No '## $tag' section in CHANGELOG.md; using a default description" }
}

# --- build
& dotnet build (Join-Path $root 'AdexansCastingMod.csproj') -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$dll = Join-Path $root 'bin\Release\netstandard2.1\AdexansCastingMod.dll'

# --- sanity: the DLL must carry the same version the tag says (the updater checks this too)
$asmVersion = [Reflection.AssemblyName]::GetAssemblyName($dll).Version
if ("$($asmVersion.Major).$($asmVersion.Minor).$($asmVersion.Build)" -ne $version) { throw "DLL version $asmVersion doesn't match $version" }

# --- stage assets with a SHA-256 checksum file
$out = Join-Path $root 'bin\publish'
New-Item -ItemType Directory -Force $out | Out-Null
$asset = Join-Path $out 'AdexansCastingMod.dll'
Copy-Item $dll $asset -Force
$sha = (Get-FileHash $asset -Algorithm SHA256).Hash.ToLower()
$shaFile = Join-Path $out 'AdexansCastingMod.dll.sha256'
Set-Content -Path $shaFile -Value "$sha  AdexansCastingMod.dll" -Encoding ASCII -NoNewline
Write-Host "SHA-256: $sha"

if ($DryRun) {
    Write-Host "Dry run: files are in $out, nothing was published." -ForegroundColor Yellow
    exit 0
}

# --- publish
$notesFile = Join-Path $out 'notes.md'
# UTF-8 without a BOM, so the notes don't start with an invisible character on GitHub
[IO.File]::WriteAllText($notesFile, $notes, (New-Object Text.UTF8Encoding($false)))

$gh = Get-Command gh -ErrorAction SilentlyContinue
if ($gh) {
    # Windows PowerShell treats anything gh prints to stderr (e.g. "release not found") as a fatal
    # error under 'Stop', so gh calls are judged by their exit code instead
    $ErrorActionPreference = 'Continue'
    & gh repo view $repo --json name *> $null
    $repoOk = ($LASTEXITCODE -eq 0)
    $ErrorActionPreference = 'Stop'
    if (-not $repoOk) {
        throw "GitHub repository $repo not found. Create it (public) first: gh repo create $repo --public  " +
              "(or check 'gh auth status' is logged in as the repo owner)."
    }

    # GitHub can't tag a repository that has no commits
    $ErrorActionPreference = 'Continue'
    & gh api "repos/$repo/commits?per_page=1" *> $null
    $hasCommits = ($LASTEXITCODE -eq 0)
    $ErrorActionPreference = 'Stop'
    if (-not $hasCommits) {
        throw "$repo has no commits yet, so GitHub can't create a release tag. Push the source code first (see README, 'Publishing an update')."
    }

    # A failed earlier run can leave a draft behind; remove it so this run can publish cleanly
    $ErrorActionPreference = 'Continue'
    $draftJson = & gh release view $tag --repo $repo --json isDraft 2> $null
    $found = ($LASTEXITCODE -eq 0)
    $ErrorActionPreference = 'Stop'
    if ($found) {
        if (($draftJson | ConvertFrom-Json).isDraft) {
            Write-Host "Removing the leftover draft of $tag from a failed run"
            $ErrorActionPreference = 'Continue'
            & gh release delete $tag --repo $repo --yes --cleanup-tag *> $null
            $ErrorActionPreference = 'Stop'
        }
        else {
            throw "Release $tag already exists. Bump the version in Plugin.cs first."
        }
    }

    $ErrorActionPreference = 'Continue'
    & gh release create $tag $asset $shaFile --repo $repo --title "Adexan's Casting Mod $tag" --notes-file $notesFile
    $created = ($LASTEXITCODE -eq 0)
    $ErrorActionPreference = 'Stop'
    if (-not $created) { throw 'gh release create failed (see the message above)' }
}
else {
    $token = $env:GITHUB_TOKEN
    if (-not $token) { throw 'Install the GitHub CLI (gh) and run "gh auth login", or set the GITHUB_TOKEN environment variable.' }
    $headers = @{ Authorization = "Bearer $token"; Accept = 'application/vnd.github+json'; 'User-Agent' = 'AdexansCastingMod-Release' }

    $existing = $null
    try { $existing = Invoke-RestMethod -Headers $headers -Uri "https://api.github.com/repos/$repo/releases/tags/$tag" } catch { }
    if ($existing) { throw "Release $tag already exists. Bump the version in Plugin.cs first." }

    $body = @{ tag_name = $tag; name = "Adexan's Casting Mod $tag"; body = $notes; draft = $false; prerelease = $false } | ConvertTo-Json
    $release = Invoke-RestMethod -Method Post -Headers $headers -Uri "https://api.github.com/repos/$repo/releases" -Body $body -ContentType 'application/json'

    foreach ($file in @($asset, $shaFile)) {
        $name = Split-Path $file -Leaf
        $uploadUrl = $release.upload_url -replace '\{.*\}$', "?name=$name"
        Invoke-RestMethod -Method Post -Headers $headers -Uri $uploadUrl -InFile $file -ContentType 'application/octet-stream' | Out-Null
    }
}

Write-Host "Published $tag. Players get it automatically the next time they start the game." -ForegroundColor Green

