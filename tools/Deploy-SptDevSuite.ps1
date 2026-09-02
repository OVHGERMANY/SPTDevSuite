[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)]
    [string] $CandidateDirectory,

    [Parameter(Mandatory)]
    [string] $GameRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$expectedArtifactIdentities = @(
    [pscustomobject]@{
        Name = 'SPTDevSuite.Contracts.dll'
        AssemblyName = 'SPTDevSuite.Contracts'
        Version = '0.2.0.0'
    },
    [pscustomobject]@{
        Name = 'SPTDevSuite.Server.dll'
        AssemblyName = 'SPTDevSuite.Server'
        Version = '0.2.0.0'
    }
)

$replaceableExistingPackages = @(
    [pscustomobject]@{
        Identity = 'SPT 4.1.2 unlock build'
        Artifacts = @(
            [pscustomobject]@{
                Name = 'SPTDevSuite.Contracts.dll'
                Length = 57344L
                SHA256 = 'C68F107C290622C400BB962A6BED4DC8D624F31FBD72D2F7294C55A123057F9B'
            },
            [pscustomobject]@{
                Name = 'SPTDevSuite.Server.dll'
                Length = 115712L
                SHA256 = '446EB373989FE7480D74DB1380E271064071CDB0DD8A18B679C62D759D1323C3'
            }
        )
    },
    [pscustomobject]@{
        Identity = 'SPT 4.1.2 foundation build'
        Artifacts = @(
            [pscustomobject]@{
                Name = 'SPTDevSuite.Contracts.dll'
                Length = 52736L
                SHA256 = '0C4C377A0BDA2BE036764DBB50E5DE2E67266D0ACFC185B777E6108FACA3432B'
            },
            [pscustomobject]@{
                Name = 'SPTDevSuite.Server.dll'
                Length = 85504L
                SHA256 = '2354127AF3A687E89AA164738492053B8854912034C9BE0233AEDC23030D36D4'
            }
        )
    }
)

$expectedServerDependencyIdentity = '"SPT.Server/4.1.3-RELEASE+ddce41c.20260820"'
$expectedRuntimeAssemblies = @(
    [pscustomobject]@{
        Name = 'SPT.Server.dll'
        Version = '4.1.3.0'
        Length = 229376L
        SHA256 = '26A297FEE4754A4BA0279B3C5CF9DB8A4F7272DFE71B564F903DD33591A995B1'
    },
    [pscustomobject]@{
        Name = 'SPTarkov.Common.dll'
        Version = '4.1.3.0'
        Length = 48128L
        SHA256 = '4E5C2E3286C07F13121974C101B58B29F9598114E0ED30F42988B702833E5081'
    },
    [pscustomobject]@{
        Name = 'SPTarkov.DI.dll'
        Version = '4.1.3.0'
        Length = 16896L
        SHA256 = 'D7515B2BA613D9BC4DC830D7F77DFF27E7AD97F0B32CF77F772DED55882A982B'
    },
    [pscustomobject]@{
        Name = 'SPTarkov.Server.Core.dll'
        Version = '4.1.3.0'
        Length = 5657600L
        SHA256 = '9DB58535DB2C2D2192980704B526BC0979006DB27D833F39F7907B5803101905'
    },
    [pscustomobject]@{
        Name = 'SemanticVersioning.dll'
        Version = '3.0.0.0'
        Length = 34816L
        SHA256 = '1EC4E9D7312678E23E40724207D871D0DD68A9518E39FE8165BEB6E5F98B0961'
    }
)

function Test-ArtifactSet {
    param(
        [Parameter(Mandatory)]
        [string] $Directory,

        [Parameter(Mandatory)]
        [object[]] $Artifacts
    )

    $entries = @(Get-ChildItem -LiteralPath $Directory -Force)
    if ($entries.Count -ne $Artifacts.Count -or $entries.Where({ $_.PSIsContainer }).Count -ne 0) {
        return $false
    }

    foreach ($artifact in $Artifacts) {
        $path = Join-Path $Directory $artifact.Name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            return $false
        }

        $info = Get-Item -LiteralPath $path
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($info.Length -ne $artifact.Length -or $hash -ne $artifact.SHA256) {
            return $false
        }
    }

    return $true
}

$candidateRoot = (Resolve-Path -LiteralPath $CandidateDirectory).Path
$gameRootPath = (Resolve-Path -LiteralPath $GameRoot).Path
$runtimeRoot = (Resolve-Path -LiteralPath (Join-Path $gameRootPath 'SPT_Runtime')).Path
$modsRoot = Join-Path $runtimeRoot 'user\mods'
$targetRoot = Join-Path $modsRoot 'SPTDevSuite'

$serverDependenciesPath = Join-Path $runtimeRoot 'SPT.Server.deps.json'
if (-not (Test-Path -LiteralPath $serverDependenciesPath -PathType Leaf)) {
    throw "Missing official SPT server dependency manifest: $serverDependenciesPath"
}

$serverDependencies = Get-Content -LiteralPath $serverDependenciesPath -Raw
if (-not $serverDependencies.Contains($expectedServerDependencyIdentity, [StringComparison]::Ordinal)) {
    throw "Runtime is not the exact supported official SPT build: expected $expectedServerDependencyIdentity."
}

foreach ($assembly in $expectedRuntimeAssemblies) {
    $assemblyPath = Join-Path $runtimeRoot $assembly.Name
    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
        throw "Missing exact SPT 4.1.3 runtime assembly: $assemblyPath"
    }

    $assemblyInfo = Get-Item -LiteralPath $assemblyPath
    $assemblyIdentity = [System.Reflection.AssemblyName]::GetAssemblyName($assemblyPath)
    $assemblyHash = (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash
    if ($assemblyIdentity.Version.ToString() -ne $assembly.Version -or
        $assemblyInfo.Length -ne $assembly.Length -or
        $assemblyHash -ne $assembly.SHA256) {
        throw "$($assembly.Name) runtime identity mismatch: version $($assemblyIdentity.Version), length $($assemblyInfo.Length), SHA-256 $assemblyHash."
    }
}

$runningProcesses = Get-Process -Name 'EscapeFromTarkov', 'EscapeFromTarkov_BE', 'SPT.Launcher', 'SPT.Server', 'cdb' -ErrorAction SilentlyContinue
if ($null -ne $runningProcesses) {
    throw "Game, launcher, server, or debugger process is running (PID $($runningProcesses.Id -join ', ')); deployment is blocked."
}

$candidateEntries = @(Get-ChildItem -LiteralPath $candidateRoot -Force)
$candidateFiles = @($candidateEntries.Where({ -not $_.PSIsContainer }))
if ($candidateEntries.Count -ne $expectedArtifactIdentities.Count -or $candidateFiles.Count -ne $expectedArtifactIdentities.Count) {
    throw "Candidate package contains $($candidateEntries.Count) entries and $($candidateFiles.Count) files; expected exactly $($expectedArtifactIdentities.Count) files and no other entries."
}

$expectedArtifacts = foreach ($artifactIdentity in $expectedArtifactIdentities) {
    $candidatePath = Join-Path $candidateRoot $artifactIdentity.Name
    if (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) {
        throw "Missing expected candidate assembly: $candidatePath"
    }

    $candidateInfo = Get-Item -LiteralPath $candidatePath
    $candidateAssembly = [System.Reflection.AssemblyName]::GetAssemblyName($candidatePath)
    $candidateHash = (Get-FileHash -LiteralPath $candidatePath -Algorithm SHA256).Hash
    if ($candidateAssembly.Name -ne $artifactIdentity.AssemblyName -or
        $candidateAssembly.Version.ToString() -ne $artifactIdentity.Version -or
        -not [string]::Equals($candidateInfo.VersionInfo.ProductVersion, '0.2.0', [StringComparison]::Ordinal)) {
        throw "$($artifactIdentity.Name) candidate identity mismatch: assembly $($candidateAssembly.Name), version $($candidateAssembly.Version), product version $($candidateInfo.VersionInfo.ProductVersion)."
    }

    [pscustomobject]@{
        Name = $artifactIdentity.Name
        Length = $candidateInfo.Length
        SHA256 = $candidateHash
    }
}
$expectedArtifacts = @($expectedArtifacts)

if (Test-Path -LiteralPath $targetRoot) {
    $targetMatches = Test-ArtifactSet -Directory $targetRoot -Artifacts $expectedArtifacts

    if ($targetMatches) {
        [pscustomobject]@{
            Status = 'AlreadyDeployed'
            Target = $targetRoot
            Files = $expectedArtifacts.Count
        }
        return
    }

    $targetIsReplaceable = $false
    foreach ($knownPackage in $replaceableExistingPackages) {
        if (Test-ArtifactSet -Directory $targetRoot -Artifacts $knownPackage.Artifacts) {
            $targetIsReplaceable = $true
            break
        }
    }

    if (-not $targetIsReplaceable) {
        throw "Deployment target has an unknown identity and will not be replaced: $targetRoot"
    }
}

$deploymentAction = if (Test-Path -LiteralPath $targetRoot) {
    'Replace the exact known package and preserve it as rollback material'
}
else {
    'Install the exact validated package'
}

if (-not $PSCmdlet.ShouldProcess($targetRoot, $deploymentAction)) {
    [pscustomobject]@{
        Status = 'Preview'
        Target = $targetRoot
        Files = $expectedArtifacts.Count
        Runtime = 'SPT 4.1.3-RELEASE+ddce41c.20260820'
        MutationPerformed = $false
    }
    return
}

New-Item -ItemType Directory -Path $modsRoot -Force | Out-Null

$stagingRoot = Join-Path $modsRoot ".SPTDevSuite.staging.$([guid]::NewGuid().ToString('N'))"
$resolvedModsRoot = [IO.Path]::GetFullPath($modsRoot).TrimEnd('\')
$resolvedStagingParent = [IO.Path]::GetFullPath((Split-Path -Parent $stagingRoot)).TrimEnd('\')
if ($resolvedStagingParent -ne $resolvedModsRoot) {
    throw "Staging path escaped the intended mods directory: $stagingRoot"
}

$rollbackRoot = $null
$movedExistingTarget = $false

try {
    New-Item -ItemType Directory -Path $stagingRoot | Out-Null
    foreach ($artifact in $expectedArtifacts) {
        Copy-Item -LiteralPath (Join-Path $candidateRoot $artifact.Name) -Destination (Join-Path $stagingRoot $artifact.Name)
    }

    foreach ($artifact in $expectedArtifacts) {
        $stagedPath = Join-Path $stagingRoot $artifact.Name
        $stagedInfo = Get-Item -LiteralPath $stagedPath
        $stagedHash = (Get-FileHash -LiteralPath $stagedPath -Algorithm SHA256).Hash
        if ($stagedInfo.Length -ne $artifact.Length -or $stagedHash -ne $artifact.SHA256) {
            throw "$($artifact.Name) staged identity mismatch: length $($stagedInfo.Length), SHA-256 $stagedHash."
        }
    }

    if (Test-Path -LiteralPath $targetRoot) {
        $rollbackRoot = Join-Path $modsRoot ".SPTDevSuite.rollback.$([DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))"
        Move-Item -LiteralPath $targetRoot -Destination $rollbackRoot
        $movedExistingTarget = $true
    }

    Move-Item -LiteralPath $stagingRoot -Destination $targetRoot
}
catch {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }

    if ($movedExistingTarget -and $null -ne $rollbackRoot -and (Test-Path -LiteralPath $rollbackRoot) -and -not (Test-Path -LiteralPath $targetRoot)) {
        Move-Item -LiteralPath $rollbackRoot -Destination $targetRoot
    }
    throw
}

[pscustomobject]@{
    Status = 'Deployed'
    Target = $targetRoot
    Files = $expectedArtifacts.Count
    Rollback = if ($null -eq $rollbackRoot) { 'No previous package existed.' } else { "Restore $rollbackRoot to $targetRoot while SPT is stopped." }
}
