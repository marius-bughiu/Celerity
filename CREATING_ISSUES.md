# Creating Roadmap Issues

This guide explains how to create all the GitHub issues for the Celerity roadmap.

## Overview

The comprehensive roadmap has been documented in `ROADMAP.md`. This directory contains tools to help you create GitHub issues for each roadmap item.

## Method 1: Using the Shell Script (Recommended)

We've provided an automated script that creates all 21 roadmap issues using the GitHub CLI.

### Prerequisites

1. Install GitHub CLI (`gh`):
   - **macOS**: `brew install gh`
   - **Linux**: See https://github.com/cli/cli#installation
   - **Windows**: See https://github.com/cli/cli#installation

2. Authenticate with GitHub:
   ```bash
   gh auth login
   ```

### Running the Script

From the repository root, run:

```bash
./create-roadmap-issues.sh
```

The script will create all 21 issues with:
- Appropriate titles
- Labels for priority, phase, and type
- Detailed descriptions with tasks
- References to the ROADMAP.md

**Note:** The script skips issue #1 (Set up github-action-benchmark) and issue #2 (Create hash function evaluator) since they already exist in the repository.

## Method 2: Manual Creation

If you prefer to create issues manually or need to customize them, refer to `ROADMAP_ISSUES.md` which contains:
- Formatted issue content ready to copy-paste
- Complete descriptions and task lists for each issue
- Labels and metadata for each issue

Simply copy each issue's content and create it through the GitHub web interface.

## Issues to be Created

The script/documentation creates **21 new issues** organized into 7 phases:

### Phase 1: Infrastructure & Tooling
- [ ] Improve documentation

### Phase 2: New Collections
- [ ] Implement CeleritySet
- [ ] Implement LongDictionary
- [ ] Implement CelerityMultiMap
- [ ] Implement CelerityList

### Phase 3: Advanced Features
- [ ] Add thread-safe collections
- [ ] Memory-pooled collections
- [ ] Implement frozen collections
- [ ] Add SIMD optimizations

### Phase 4: Additional Hash Functions
- [ ] Add more hash function implementations
- [ ] Adaptive hash function selection

### Phase 5: Performance & Quality
- [ ] Comprehensive benchmark suite expansion
- [ ] Performance optimizations
- [ ] Cross-platform testing
- [ ] Code coverage improvements

### Phase 6: Advanced Use Cases
- [ ] Specialized collections for specific domains
- [ ] Serialization support
- [ ] Native AOT support

### Phase 7: Ecosystem & Community
- [ ] Community engagement
- [ ] Educational content
- [ ] Integration examples

## After Creating Issues

Once issues are created, you can:
1. Organize them using GitHub Projects
2. Add them to milestones based on release plans
3. Assign team members as appropriate
4. Start working on high-priority items

## Questions or Issues?

If you encounter any problems with the script or have questions about the roadmap, please open a discussion on GitHub.
