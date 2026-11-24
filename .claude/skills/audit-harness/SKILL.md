---
name: audit-harness
description: Audit the project's AI setup — fixed context size, redundant or conflicting instructions, permissions, security — and produce a reduction plan. Use when a month has passed since the last audit, after large changes to AGENTS.md/skills, or when the agent starts ignoring rules.
---

# Harness audit

## 1. Measure (deterministic)
```bash
~/ai-standard/tools/audit.sh .
```
Issues (✗) — fix. Warnings (!) — evaluate.

## 2. Judge the content (instruction debt) — read AGENTS.md, CLAUDE.md, rules, skill descriptions
Find lines that:
- **waste context** — the agent already does it or can infer it from code → delete;
- **load needlessly** — rarely needed → move to a skill or a rule with `paths:`;
- **conflict** — two rules contradict or duplicate each other → keep one;
- **stop too early** — no verification command or definition of done → add;
- **grant too much** — broad allow, no deny for secrets and prod → narrow.
Pick the smallest interface: script → skill → subagent → hook → MCP (remote services only).

## 3. Plan
Table: what → why → action → savings (~tokens). Security issues first, then the biggest savings.

## 4. Apply and verify
- Changes in a separate commit `chore(ai): audit harness`.
- Re-run `audit.sh` — show before/after.
- A structural check proves only what it inspected: confirm agent behavior with 1–2 typical tasks (run `evals/` if present).
- One-line outcome in `docs/DECISIONS.md`.
