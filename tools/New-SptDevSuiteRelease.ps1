[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$buildRoot = Join-Path $repositoryRoot 'Build'
$packageRoot = Join-Path $buildRoot 'SPTDevSuite'
$readmePath = Join-Path $repositoryRoot 'packaging\README.txt'
$archivePath = Join-Path $buildRoot 'SPTDevSuite-0.2.0-SPT-4.1.3.zip'
$sidecarPath = "$archivePath.sha256"
$fixedTimestamp = [DateTimeOffset]::new(2026, 9, 1, 0, 0, 0, [TimeSpan]::Zero)

$runtimeFiles = @(
    [pscustomobject]@{
        Name = 'SPTDevSuite.Contracts.dll'
        Entry = 'SPT_Runtime/user/mods/SPTDevSuite/SPTDevSuite.Contracts.dll'
    },
    [pscustomobject]@{
        Name = 'SPTDevSuite.Server.dll'
        Entry = 'SPT_Runtime/user/mods/SPTDevSuite/SPTDevSuite.Server.dll'
    }
)

if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
    throw "Release package directory does not exist: $packageRoot"
}

if (-not (Test-Path -LiteralPath $readmePath -PathType Leaf)) {
    throw "Release instructions do not exist: $readmePath"
}

$packageEntries = @(Get-ChildItem -LiteralPath $packageRoot -Force)
$packageFiles = @($packageEntries | Where-Object { -not $_.PSIsContainer })
if ($packageEntries.Count -ne $runtimeFiles.Count -or $packageFiles.Count -ne $runtimeFiles.Count) {
    throw "Runtime package must contain exactly $($runtimeFiles.Count) files and no directories; found $($packageEntries.Count) entries."
}

$releaseEntries = @(
    [pscustomobject]@{
        Source = $readmePath
        Entry = 'README.txt'
    }
)

foreach ($runtimeFile in $runtimeFiles) {
    $sourcePath = Join-Path $packageRoot $runtimeFile.Name
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Required runtime file is missing: $sourcePath"
    }

    $releaseEntries += [pscustomobject]@{
        Source = $sourcePath
        Entry = $runtimeFile.Entry
    }
}

if (-not (Test-Path -LiteralPath $buildRoot -PathType Container)) {
    New-Item -ItemType Directory -Path $buildRoot | Out-Null
}

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

$archiveStream = [IO.File]::Open($archivePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
$archive = [IO.Compression.ZipArchive]::new($archiveStream, [IO.Compression.ZipArchiveMode]::Create, $false)
try {
    foreach ($releaseEntry in $releaseEntries) {
        $entry = $archive.CreateEntry($releaseEntry.Entry, [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = $fixedTimestamp
        $inputStream = [IO.File]::OpenRead($releaseEntry.Source)
        $outputStream = $entry.Open()
        try {
            $inputStream.CopyTo($outputStream)
        }
        finally {
            $outputStream.Dispose()
            $inputStream.Dispose()
        }
    }
}
finally {
    $archive.Dispose()
    $archiveStream.Dispose()
}

$expectedHashes = @{}
foreach ($releaseEntry in $releaseEntries) {
    $expectedHashes[$releaseEntry.Entry] = (Get-FileHash -LiteralPath $releaseEntry.Source -Algorithm SHA256).Hash
}

$validation = @()
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $actualNames = @($archive.Entries | ForEach-Object { $_.FullName })
    $expectedNames = @($releaseEntries | ForEach-Object { $_.Entry })
    if (@(Compare-Object -ReferenceObject $expectedNames -DifferenceObject $actualNames).Count -ne 0) {
        throw 'Release archive entry set does not match the allowlist.'
    }

    foreach ($entry in $archive.Entries | Sort-Object FullName) {
        $entryStream = $entry.Open()
        try {
            $entryHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($entryStream))
        }
        finally {
            $entryStream.Dispose()
        }

        $byteIdentical = $entryHash -eq $expectedHashes[$entry.FullName]
        if (-not $byteIdentical) {
            throw "Release archive byte verification failed: $($entry.FullName)"
        }

        $validation += [pscustomobject]@{
            Entry = $entry.FullName
            Length = $entry.Length
            SHA256 = $entryHash
            ByteIdentical = $byteIdentical
        }
    }
}
finally {
    $archive.Dispose()
}

$archiveInfo = Get-Item -LiteralPath $archivePath
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
[IO.File]::WriteAllText(
    $sidecarPath,
    "$archiveHash  $($archiveInfo.Name)`r`n",
    [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    Status = 'ReleaseCandidateCreated'
    Archive = $archiveInfo.FullName
    Length = $archiveInfo.Length
    SHA256 = $archiveHash
    Sidecar = $sidecarPath
    Entries = $validation
}
