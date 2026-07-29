#!/usr/bin/env bash
#
# extract-release-notes.sh — pull one version's section out of CHANGELOG.md (#315).
#
# The release workflow uses the extracted section verbatim as the GitHub Release
# body. Both failure modes below used to surface only in the `github-release`
# job, which runs *after* `dotnet nuget push` — so a bad CHANGELOG meant six
# packages irreversibly on NuGet.org and no release to go with them. This script
# is called from the `build` job instead, before anything is published.
#
# Fails when:
#   * the CHANGELOG has no "## [<version>]" section (the block was never
#     promoted out of [Unreleased]); or
#   * the section is larger than GitHub's release-body cap. `## [1.5.0]` was
#     183,300 chars and `## [2.0.0]` was 133,453 — this repo overruns the cap in
#     practice, which is why CONTRIBUTING.md calls terse entries a release-safety
#     rule rather than a style preference.
#
# Usage: extract-release-notes.sh <version> [changelog-path] [output-path]
#   e.g. ./.github/scripts/extract-release-notes.sh 2.4.0
#
# Runnable locally against any version to check a section before tagging.

set -euo pipefail

version="${1:-}"
changelog="${2:-CHANGELOG.md}"
output="${3:-release-notes.md}"

# GitHub caps release bodies at ~125,000 characters. Assert on bytes, which for
# UTF-8 is never fewer than characters, so the check errs on the safe side.
MAX_BYTES=120000

if [ -z "$version" ]; then
    echo "usage: $0 <version> [changelog-path] [output-path]" >&2
    exit 2
fi

if [ ! -f "$changelog" ]; then
    echo "::error::Changelog not found: $changelog" >&2
    exit 1
fi

# Extract the section under "## [<version>]" up to (but not including) the next
# "## [" heading. We use index() rather than regex because matching a literal "["
# in awk's POSIX regex is fiddly across implementations.
awk -v ver="$version" '
  index($0, "## [" ver "]") == 1 { capture = 1; next }
  capture && index($0, "## [") == 1 { exit }
  capture { print }
' "$changelog" > "$output"

if [ ! -s "$output" ]; then
    echo "::error::No $changelog section found for [$version]. Move the [Unreleased] block to [$version] and re-tag." >&2
    rm -f "$output"
    exit 1
fi

bytes=$(wc -c < "$output" | tr -d '[:space:]')
if [ "$bytes" -ge "$MAX_BYTES" ]; then
    echo "::error::Release notes for [$version] are $bytes bytes, at or over the $MAX_BYTES-byte guard for GitHub's ~125k release-body cap. Condense the CHANGELOG section (see CONTRIBUTING.md, 'Changelog entries') and re-tag." >&2
    rm -f "$output"
    exit 1
fi

echo "Release notes for [$version]: $bytes bytes (limit $MAX_BYTES)."
