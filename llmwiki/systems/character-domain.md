# Character domain

Stage 05's pure C# character rules live in `lib/Runtime/Domain/Character/` and depend only on Domain config, the Shared result types, and the Arena contract. Unity-facing code should consume these types through Application/Presentation boundaries; this system note describes the current contract, not UI behavior.

## State and modifiers

- `CharacterState` owns base attributes, learned skill progress, gear inventory, loadout, background id, unspent creation points and the dirty/cached `CombatStatBlock` state.
- Mutations to attributes, skill progress, inventory, modifiers or loadout mark derived stats dirty.
- `ModifierStack` retains each `{attribute, value, source kind, source id}` contribution. Duplicate source ids remain separate rows so tooltips can preserve each contribution; arithmetic uses checked integer sums.
- `StatResolver` composes base attributes, explicit modifiers, learned skill-level bonuses and equipped gear mods. Only equipped gear contributes. Its `Span` copy APIs write into the caller's buffer.

## Derived profiles

`stat_formula` is the sole source for `CombatStatBlock` values. Terms are multiplied and clamped individually, summed with the base value, then the result is clamped. A resolved block is cached until the character becomes dirty. `PowerScore` sums each attribute times its authored `power_weight`; the nine ranks use the strictly increasing `constant.power_rank_2_min` through `_9_min` thresholds. `RadarProfile` is the weighted integer average for Martial, Neili, Body, Mind and Meridian, using `attribute.radar_weight`.

Creation bounds, power/radar weights, XP rate and rank cutoffs are Luban-authored values. `ConfigValidator` checks ordered creation bounds, nonnegative weights, display-only power contribution, rank threshold order and mastery progression order.

## Loadout and arena conversion

`LoadoutRules` validates owned gear and learned skills against config slot/kind rules. There are four external slots and one inner, one movement, one weapon, one armor and two accessory slots. An external skill incompatible with the currently equipped weapon returns a typed failure when equipped; changing the weapon does not erase it, but reports it disabled. `ArenaFighterBuilder` validates the loadout and emits enabled external skills in slot order plus the Arena contract's passive identifiers and resolved combat stats.

## Creation and mastery

`CharacterCreation` applies a selected background's starting attributes, skills, gear, loadout and point pool. `Allocate` enforces per-attribute Luban bounds and the available pool; `Validate` also checks that a valid background and required loadout remain. `StatGrowth.ApplyMastery` consumes `SkillUseStat` values, validates the full input before changing progress, awards config-authored XP multiplied by the authored Intellect rate, then advances through every reached mastery threshold. Skill ids use `StatGrowth.StableId`, the same deterministic mapping used when Arena results are produced.

## Verification

Stage evidence and exact commands are in [`../../docs/stages/05-report.md`](../../docs/stages/05-report.md). Coverage is measured with `coverage-domain.runsettings` and `tools/coverage-report.mjs`; the Character folder aggregate in the Stage 05 run was 512/560 lines (91.43%).
