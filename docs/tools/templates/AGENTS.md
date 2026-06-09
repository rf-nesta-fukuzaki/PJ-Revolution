# [Project Name] — Agent Instructions

<!-- 
  PROJECT-SPECIFIC AGENTS.md TEMPLATE (ECC v1.10.0)
  
  This file extends ~/AGENTS.md (global ECC rules) with project-specific context.
  Codex and other agents load both: ~/AGENTS.md first, then this file.
  
  Instructions:
  1. Replace [Project Name] in the heading above
  2. Fill in the sections below that are relevant to this project
  3. Delete any sections that don't apply
  4. Commit this file to the repo root
-->

## Project Overview

**Purpose:** [One-sentence description of what this project does]  
**Stack:** [e.g. Next.js 14 · TypeScript · Supabase · Tailwind CSS]  
**Status:** [Active development / Maintenance / Production]

---

## Key Directories

```
src/
├── [describe your main dirs]
```

---

## Environment Setup

```bash
# Required environment variables (see .env.example)
[LIST_KEY_ENV_VARS]

# Install & run
[INSTALL_COMMAND]
[DEV_COMMAND]
```

---

## Architecture Decisions

<!-- Document any project-specific patterns that deviate from or extend the global rules -->

- **[Decision]:** [Why]
- **[Decision]:** [Why]

---

## Project-Specific Conventions

<!-- Only add rules that are genuinely specific to this project -->

### Naming

- [Any project-specific naming rules]

### File Structure

- [Any project-specific structure rules]

---

## Testing

```bash
[TEST_COMMAND]      # unit tests
[E2E_COMMAND]       # E2E tests (if applicable)
```

Coverage target: 80%+ (inherited from global rules)

---

## Important Files

| File | Purpose |
|------|---------|
| [path] | [description] |

---

## Out of Scope / Do Not Touch

- [List files or areas that should not be modified by agents]

---

<!-- 
  Global rules (~/AGENTS.md) cover:
  - Core principles (Agent-First, TDD, Security-First, Immutability, Plan Before Execute)
  - Coding style (KISS/DRY/YAGNI, immutability, naming, file organization)
  - Security guidelines and checklists
  - Testing requirements (80% coverage, TDD workflow, AAA pattern)
  - Development workflow (research → plan → TDD → review → commit)
  - Git workflow (conventional commits, PR process)
  - Web rules (CSS tokens, animation, semantic HTML, CWV targets, design quality)
  - Full ECC agent roster (47 agents)
  Do NOT duplicate those here.
-->
