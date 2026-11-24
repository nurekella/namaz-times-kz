---
name: spec-interview
description: Interview the human and write a complete specification to docs/SPEC.md before a large task. Use when a task is large, unclear, or touches many systems.
---

# Spec interview
1. Ask questions one block at a time (use the question tool if available): goal, constraints, environments, access, edge cases, rollback, out of scope. Skip the obvious — dig into the hard parts.
2. Keep going until every point is covered.
3. Write `docs/SPEC.md`:
   - Goal and definition of done
   - Affected files, services, interfaces
   - Out of scope
   - Tasks: small (≤ ~200 lines of diff), ordered, each with acceptance criteria
   - Risks and rollback plan
   - **End-to-end final verification** (commands)
4. Completeness check (before handing to an agent or starting work) — SPEC has: definition of done, verification commands, out of scope, affected files/hosts, rollback, required access (where, not values). Fill gaps or ask.
5. Suggest starting implementation in a new session (`/clear`) via skill `implement`.
