#!/usr/bin/env pwsh
#
# validate-packages.ps1 — release-gate validation for the Celerity package family (#190).
#
# Asserts that `dotnet pack` produced a correct, publishable set of packages
# before any of them is pushed to NuGet.org:
#
#   * exactly the expected shipped packages are present — the three core family
#     packages (Celerity.Collections / Celerity.Hashing / Celerity.Primitives)
#     plus the "built with Celerity" showcase packages (Celerity.Ring /
#     Celerity.Sentinel / Celerity.Cardinality) — and no unexpected extra package
#     (e.g. a dev project that lost its IsPackable=false);
#   * every .nupkg has a matching .snupkg symbol package;
#   * every .nupkg carries the required NuGet metadata — a license, a README, an
#     icon, and a <repository> URL with a commit SHA (the SourceLink stamp);
#   * the README and icon files are actually embedded in the .nupkg.
#
# Exits non-zero with a clear message on the first failure so a mis-packed
# release never reaches the symbol server or NuGet.org.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$NuGetDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$expectedPackages = @(
    # Core family (release in lockstep).
    'Celerity.Collections',
    'Celerity.Hashing',
    'Celerity.Primitives',
    # "Built with Celerity" showcase tier — standalone libraries built on top of
    # the core family; packable, so solution-wide `dotnet pack` emits them too.
    'Celerity.Ring',
    'Celerity.Sentinel',
    'Celerity.Cardinality'
)

if (-not (Test-Path $NuGetDirectory)) {
    throw "NuGet output directory not found: $NuGetDirectory"
}

$nupkgs = @(Get-ChildItem -Path $NuGetDirectory -Filter '*.nupkg' -Recurse |
    Where-Object { $_.Name -notlike '*.snupkg' })

if ($nupkgs.Count -eq 0) {
    throw "No .nupkg files found in $NuGetDirectory."
}

$errors = New-Object System.Collections.Generic.List[string]
$seenIds = New-Object System.Collections.Generic.List[string]

function Get-EntryText {
    param(
        [System.IO.Compression.ZipArchive]$Archive,
        [string]$EntryName
    )
    $entry = $Archive.Entries | Where-Object { $_.FullName -eq $EntryName } | Select-Object -First 1
    if ($null -eq $entry) { return $null }
    $reader = New-Object System.IO.StreamReader($entry.Open())
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}

foreach ($nupkg in $nupkgs) {
    Write-Host "Validating $($nupkg.Name)"

    # 1. Matching symbol package.
    $snupkg = [System.IO.Path]::ChangeExtension($nupkg.FullName, '.snupkg')
    if (-not (Test-Path $snupkg)) {
        $errors.Add("$($nupkg.Name): missing symbol package ($([System.IO.Path]::GetFileName($snupkg))).")
    }

    $zip = [System.IO.Compression.ZipFile]::OpenRead($nupkg.FullName)
    try {
        $nuspecEntry = $zip.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
        if ($null -eq $nuspecEntry) {
            $errors.Add("$($nupkg.Name): no .nuspec found inside the package.")
            continue
        }

        $reader = New-Object System.IO.StreamReader($nuspecEntry.Open())
        try { $nuspecXml = [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }

        $ns = New-Object System.Xml.XmlNamespaceManager($nuspecXml.NameTable)
        $defaultNs = $nuspecXml.package.xmlns
        if ([string]::IsNullOrEmpty($defaultNs)) { $defaultNs = 'http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd' }
        $ns.AddNamespace('n', $defaultNs)

        $meta = $nuspecXml.SelectSingleNode('//n:metadata', $ns)
        $id = $meta.SelectSingleNode('n:id', $ns).InnerText
        $seenIds.Add($id)

        # 2. Required metadata.
        $license = $meta.SelectSingleNode('n:license', $ns)
        if ($null -eq $license -or [string]::IsNullOrWhiteSpace($license.InnerText)) {
            $errors.Add("${id}: missing <license> metadata.")
        }

        $readme = $meta.SelectSingleNode('n:readme', $ns)
        if ($null -eq $readme -or [string]::IsNullOrWhiteSpace($readme.InnerText)) {
            $errors.Add("${id}: missing <readme> metadata.")
        }

        $icon = $meta.SelectSingleNode('n:icon', $ns)
        if ($null -eq $icon -or [string]::IsNullOrWhiteSpace($icon.InnerText)) {
            $errors.Add("${id}: missing <icon> metadata.")
        }

        $projectUrl = $meta.SelectSingleNode('n:projectUrl', $ns)
        if ($null -eq $projectUrl -or [string]::IsNullOrWhiteSpace($projectUrl.InnerText)) {
            $errors.Add("${id}: missing <projectUrl> metadata.")
        }

        # 3. Repository URL + commit (the SourceLink / deterministic-build stamp).
        $repo = $meta.SelectSingleNode('n:repository', $ns)
        if ($null -eq $repo) {
            $errors.Add("${id}: missing <repository> metadata (PublishRepositoryUrl).")
        }
        else {
            if ([string]::IsNullOrWhiteSpace($repo.GetAttribute('url'))) {
                $errors.Add("${id}: <repository> has no url attribute.")
            }
            if ([string]::IsNullOrWhiteSpace($repo.GetAttribute('commit'))) {
                $errors.Add("${id}: <repository> has no commit attribute (SourceLink not active).")
            }
        }

        # 4. The README/icon files are actually embedded.
        if ($null -ne $readme -and -not [string]::IsNullOrWhiteSpace($readme.InnerText)) {
            $readmeName = $readme.InnerText
            if (-not ($zip.Entries | Where-Object { $_.FullName -eq $readmeName })) {
                $errors.Add("${id}: declared readme '$readmeName' is not embedded in the package.")
            }
        }
        if ($null -ne $icon -and -not [string]::IsNullOrWhiteSpace($icon.InnerText)) {
            $iconName = $icon.InnerText
            if (-not ($zip.Entries | Where-Object { $_.FullName -eq $iconName })) {
                $errors.Add("${id}: declared icon '$iconName' is not embedded in the package.")
            }
        }
    }
    finally {
        $zip.Dispose()
    }
}

# 5. Exactly the three expected packages — no missing, no surprise extra.
foreach ($expected in $expectedPackages) {
    if ($seenIds -notcontains $expected) {
        $errors.Add("Expected package '$expected' was not produced.")
    }
}
foreach ($seen in $seenIds) {
    if ($expectedPackages -notcontains $seen) {
        $errors.Add("Unexpected package '$seen' was produced (a dev project may have lost IsPackable=false).")
    }
}

if ($errors.Count -gt 0) {
    Write-Host ''
    Write-Host '::error::Package validation failed:'
    foreach ($e in $errors) {
        Write-Host "::error::  - $e"
    }
    exit 1
}

Write-Host ''
Write-Host "Package validation passed: $($nupkgs.Count) package(s), all with symbols, metadata, and SourceLink."
