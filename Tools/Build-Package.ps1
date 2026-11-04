param([string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$packagePath = 'Assets/Packages/UIWhiteboxTool'
$source = Join-Path $repo $packagePath
$stage = Join-Path ([IO.Path]::GetTempPath()) ('UIWhitebox-Pack-' + [guid]::NewGuid().ToString('N'))
$payload = Join-Path $stage 'payload'
$verify = Join-Path $stage 'verify'
New-Item -ItemType Directory -Path $payload,$verify -Force | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)
$expected = @{}
$guids = @{}
$metas = @((Get-Item -LiteralPath ($source + '.meta'))) + @(Get-ChildItem -LiteralPath $source -Recurse -Filter '*.meta' -File | Where-Object { $_.FullName -notmatch '[\\/]Editor[\\/]Data([\\/.])|[\\/]Generated([\\/.])' })
foreach ($meta in $metas) {
    $asset = $meta.FullName.Substring(0, $meta.FullName.Length - 5)
    $path = $asset.Substring($repo.Length + 1).Replace('\','/')
    if ($path -ne $packagePath -and -not $path.StartsWith($packagePath + '/')) { throw "Path outside package: $path" }
    $metadata = [IO.File]::ReadAllText($meta.FullName)
    $match = [regex]::Match($metadata, '(?m)^guid: ([a-f0-9]{32})\r?$')
    if (-not $match.Success) { throw "Invalid GUID: $path" }
    $guid = $match.Groups[1].Value
    if ($guids.ContainsKey($guid)) { throw "Duplicate GUID: $guid" }
    $guids[$guid] = $path
    $entry = Join-Path $payload $guid; New-Item -ItemType Directory -Path $entry | Out-Null
    [IO.File]::WriteAllText((Join-Path $entry 'pathname'), $path, $utf8)
    [IO.File]::WriteAllText((Join-Path $entry 'asset.meta'), $metadata, $utf8)
    if (Test-Path -LiteralPath $asset -PathType Leaf) {
        Copy-Item -LiteralPath $asset -Destination (Join-Path $entry 'asset')
        $expected[$guid] = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash
    }
}
$release = Join-Path $repo ('Releases/' + $Version); New-Item -ItemType Directory -Path $release -Force | Out-Null
$output = Join-Path $release ('UIWhiteboxTool-' + $Version + '-Basic.unitypackage')
$tar = (Get-Command tar.exe -ErrorAction SilentlyContinue).Source
if (-not $tar -and (Test-Path -LiteralPath 'C:\Windows\System32\tar.exe')) { $tar = 'C:\Windows\System32\tar.exe' }
if (-not $tar) { $tar = (Get-Command tar -ErrorAction Stop).Source }
& $tar -czf $output -C $payload .
if ($LASTEXITCODE -ne 0) { throw 'Archive creation failed' }
& $tar -xzf $output -C $verify
if ($LASTEXITCODE -ne 0) { throw 'Archive verification failed' }
$entries = @(Get-ChildItem -LiteralPath $verify -Directory)
if ($entries.Count -ne $guids.Count) { throw 'GUID count mismatch' }
foreach ($entry in $entries) {
    if ([IO.File]::ReadAllText((Join-Path $entry.FullName 'pathname')) -ne $guids[$entry.Name]) { throw 'Archived path mismatch' }
    if ($expected.ContainsKey($entry.Name) -and (Get-FileHash -LiteralPath (Join-Path $entry.FullName 'asset') -Algorithm SHA256).Hash -ne $expected[$entry.Name]) { throw 'Archived file hash mismatch' }
}
$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $release 'SHA256SUMS.txt'), ($hash + '  ' + [IO.Path]::GetFileName($output) + "`n"), $utf8)
Write-Output ('PASS: package containment, ' + $entries.Count + ' GUID entries, ' + $expected.Count + ' file hashes.')
Get-Item -LiteralPath $output | Select-Object FullName,Length
