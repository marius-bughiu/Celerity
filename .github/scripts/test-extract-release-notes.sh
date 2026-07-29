#!/usr/bin/env bash
#
# test-extract-release-notes.sh — regression tests for extract-release-notes.sh (#315).
#
# Runs in CI on every PR. The oversized-section case is the regression test for
# the bug in #315: before that fix the release workflow had no size check at all,
# so a CHANGELOG section over GitHub's ~125k release-body cap published six
# packages to NuGet.org and only then failed to create the release. Case 3 below
# fails against that behaviour.
#
# Usage: ./.github/scripts/test-extract-release-notes.sh

set -uo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
extract="$script_dir/extract-release-notes.sh"
workdir="$(mktemp -d)"
trap 'rm -rf "$workdir"' EXIT

failures=0

# assert <name> <expected-exit> <version> <changelog> [extra-check...]
assert_exit() {
    local name="$1" expected="$2" version="$3" changelog="$4"
    local out="$workdir/out.md"
    rm -f "$out"
    "$extract" "$version" "$changelog" "$out" > "$workdir/stdout.txt" 2> "$workdir/stderr.txt"
    local actual=$?
    if [ "$actual" -ne "$expected" ]; then
        echo "FAIL: $name — expected exit $expected, got $actual"
        sed 's/^/      /' "$workdir/stderr.txt"
        failures=$((failures + 1))
        return 1
    fi
    echo "pass: $name (exit $actual)"
    return 0
}

# A changelog with a small section, a large section, and a decoy that must not
# bleed into the section above it.
big_changelog="$workdir/CHANGELOG.big.md"
{
    echo "# Changelog"
    echo
    echo "## [Unreleased]"
    echo
    echo "- pending"
    echo
    echo "## [2.0.0]"
    echo
    # ~130k of body: over the 120000-byte guard, under no other limit.
    for _ in $(seq 1 1300); do
        printf -- '- %s\n' "$(printf 'x%.0s' $(seq 1 96))"
    done
    echo
    echo "## [1.0.0]"
    echo
    echo "- Initial release."
} > "$big_changelog"

small_changelog="$workdir/CHANGELOG.small.md"
{
    echo "# Changelog"
    echo
    echo "## [1.0.0]"
    echo
    echo "### Added"
    echo
    echo "- A thing."
    echo
    echo "## [0.9.0]"
    echo
    echo "- An older thing that must NOT be captured."
} > "$small_changelog"

echo "== extract-release-notes.sh =="

# 1. Happy path: the section is extracted and stops at the next heading.
if assert_exit "extracts a well-sized section" 0 1.0.0 "$small_changelog"; then
    if grep -q "older thing" "$workdir/out.md"; then
        echo "FAIL: capture bled into the next ## [ section"
        failures=$((failures + 1))
    elif ! grep -q "A thing." "$workdir/out.md"; then
        echo "FAIL: section body missing from the output"
        failures=$((failures + 1))
    else
        echo "pass: section is bounded by the next ## [ heading"
    fi
fi

# 2. No section for the tag's version — the pre-existing check.
assert_exit "rejects a missing section" 1 9.9.9 "$small_changelog"

# 3. Section over GitHub's release-body cap — the #315 regression test.
assert_exit "rejects an oversized section" 1 2.0.0 "$big_changelog"

# 4. A failed run must not leave a partial release-notes.md behind for a later
#    step to publish.
if [ -f "$workdir/out.md" ]; then
    echo "FAIL: output file left behind after a failing run"
    failures=$((failures + 1))
else
    echo "pass: no output file left behind after a failing run"
fi

# 5. Missing changelog path.
assert_exit "rejects a missing changelog file" 1 1.0.0 "$workdir/nope.md"

# 6. No version argument at all.
rm -f "$workdir/out.md"
"$extract" > /dev/null 2>&1
if [ $? -eq 2 ]; then
    echo "pass: rejects a missing version argument (exit 2)"
else
    echo "FAIL: expected exit 2 with no version argument"
    failures=$((failures + 1))
fi

echo
if [ "$failures" -ne 0 ]; then
    echo "::error::$failures release-notes gate test(s) failed."
    exit 1
fi
echo "All release-notes gate tests passed."
