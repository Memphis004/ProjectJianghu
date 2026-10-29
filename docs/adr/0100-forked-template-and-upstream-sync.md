# ADR 0100 — This repository is a fork of `template-v1`; the template is synced through `upstream/template`

- **Status:** Accepted
- **Date:** 2026-10-29
- **Stage:** 00
- **Supersedes:** nothing
- **Applies to:** every later stage

## Context

ProjectJianghu is not a template any more. The tree was copied from `template-v1` (the
genre-free skeleton: `lib/` layers, `data/` pipeline, six-scene Unity shell, the tooling
and the docs), renamed with `tools/rename.ps1`, and made to boot — that last step is
[`j-boot-report.md`](../stages/j-boot-report.md). From here the repository is the game.

Two facts force a decision now:

1. **The template improves on its own schedule.** `docs/template/README.md` ("Upgrade a
   game from the template") says template commits are written with a `generic:` prefix
   so a game can take them. That is only true if the game has a rule for taking them.
2. **The two lines describe the same files.** `AGENTS.md`, `knowledge.md`,
   `llmwiki/*`, `docs/architecture/*` and `docs/testing/*` exist on both sides. A merge
   of the template line would overwrite the game's rules with the template's, and the
   conflict is not resolvable by "which side is newer".

The repository already carries the split, without a rule saying what it means:

| Ref | Commit | What it is |
| --- | --- | --- |
| `main` | `378d8dc` `bootstrap: unity import + verification` | the game line (this repository's history) |
| `upstream/template` (local and `origin`) | `6e64295` `upstream: template-v1 renamed to ProjectJianghu` | the template line, as imported |
| `tpl-v1` (tag) | `6e64295` | the fork point: the exact tree this game started from |
| — | `c09deab` `docs: record template origin` | the game's provenance commit (`docs/template/ORIGIN.md`) |

`tpl-v1`, `upstream/template` and `origin/upstream/template` are all the same commit
today, so the template line currently holds **no** post-fork work. This ADR fixes how
that changes.

## Decision

1. **`main` is the game.** All work lands there. `upstream/template` is a read-only
   mirror of the template line, kept on `origin` so a clone can see it; nobody commits
   to it from this repository.
2. **Never merge, never rebase onto it.** `git merge upstream/template` and
   `git rebase upstream/template` are both forbidden: they would drag
   `docs/template/**` (the extraction's history) into the game's line and rewrite the
   game's own documents. The template is a *source of commits*, not a parent.
3. **Take only `generic:` commits.** A commit whose subject starts `generic:` is
   written against no game type (AGENTS #16) and is the only kind eligible. Take it with
   `git cherry-pick -x <sha>` so the provenance stays in the game's history.
4. **Do not take:**
   - anything under `docs/template/` (including `examples/`) — it is the template's own
     plan, reports and archive;
   - any wholesale copy of `tools/versions.lock.json` — the lock records what *this*
     repository verified, so a version bump is a decision, not a cherry-pick;
   - a `llmwiki/stage-log.md` row, or an edit to this file, `AGENTS.md`, `knowledge.md`
     or `docs/stages/**`.
5. **Conflicts in a shared document are resolved by rule, not by side.** If a `generic:`
   commit changes the layer matrix, the data pipeline, a test suite or a gotcha, read
   the rule it establishes and re-apply it to the game's version of the document by
   hand. `git checkout --theirs knowledge.md` is not an upgrade; it is a rollback of
   every game decision in that file.
6. **A `generic:` commit that touches a renamed identifier is applied as a patch.**
   `tools/rename.ps1 -PatchFile <file>` rewrites a diff for this identity; use it
   instead of hand-editing after a failed cherry-pick.
7. **ADR numbering.** 0001–0099 are inherited from the template and stay as they are,
   including their own `Inherited by:` lines. The game's decisions start at **0100**
   (AGENTS #14); this is the first.
8. **Gates after every sync**, in this order: `tools/test.ps1 -Suite Fast`; plus
   `tools/verify-generated.ps1` and `tools/check-fixtures.ps1` when `data/` or `proto/`
   moved; plus `-Suite Full` at the next stage boundary. A template change that compiles
   but shifts the config hash is exactly what those gates are for.

### The sync, as commands

```powershell
git fetch origin
# what is new on the template line since the fork, generic only
git log --oneline tpl-v1..origin/upstream/template --grep '^generic:'
# take one at a time, oldest first
git cherry-pick -x <sha>
# if a renamed identifier conflicts, rewrite the diff instead of resolving by hand
pwsh -NoProfile -File tools/rename.ps1 -From <template-name> -To <game-name> -PatchFile <patch>
pwsh -NoProfile -File tools/test.ps1 -Suite Fast
```

## Alternatives considered

- **Merge `upstream/template` into `main` regularly** (the usual fork model). Rejected:
  every merge conflicts in the six shared documents, and the resolution is a manual
  re-application anyway — the merge buys nothing and risks silently reverting game rules.
- **Make `lib/` a submodule or a package dependency on the template.** Rejected: the
  game's `Domain` *is* `lib/Runtime/Domain`, and its ASMDEFs and guard tests live next to
  the shell's. Splitting it at the assembly level would be a restructure this stage has
  no reason to do.
- **Copy template fixes by hand with no history.** Rejected: it loses the ability to
  tell "already taken" from "not taken yet", and the `generic:` prefix exists precisely
  to make that answer a `git log`.
- **Freeze the fork and never sync.** Rejected: the template still owns the build/test
  tooling and the gotchas, and a tool fix we cannot take is a defect we keep.

## Consequences

- **Positive:** the game's documents have one owner; a template fix arrives as a small,
  reviewable commit with `-x` provenance; drift is one command to measure
  (`git rev-list --count tpl-v1..origin/upstream/template`).
- **Negative:** keeping `upstream/template` means two branches on `origin` that look
  similar and must not be confused; the `generic:` prefix is now load-bearing and must
  be enforced by review on the template side.
- **Risk:** a `generic:` commit can be *logically* incompatible with a game decision
  even though it applies cleanly (a tool rewritten against the placeholder loop). The
  gates catch a broken build, not a wrong rule; read the diff.
- **Follow-up:** if `upstream/template` starts carrying non-`generic:` commits that we
  need, the answer is a new ADR, not a merge.
