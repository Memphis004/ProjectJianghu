/**
 * The page globals every spec reads — defined once, here (Stage 10t).
 *
 * The C# side has one point of the same names (`WebGlBridge`, and one report class per
 * screen), which passes them to the `.jslib` bridge; a rename on that side must be made
 * here as well, and this file is the only place on the browser-test side that a name is
 * written down. Every `*.spec.ts` imports from it, and so does
 * `scripts/probe-boot.mjs`, so a re-export cannot leave one reader looking at a global
 * nobody publishes any more.
 *
 * Plain ESM rather than `.ts` because the diagnostic script is plain node: a `.mjs` file
 * is what both a Playwright spec and `node scripts/probe-boot.mjs` can import without a
 * build step.
 *
 *   __projectJianghuSmoke           the boot self-check report (`SceneBoot`)
 *   __projectJianghuScene           the scene on screen (every scene director)
 *   __projectJianghuThaiSpike       the Thai text spike measurement (`ThaiSpikeReporter`)
 *   __projectJianghuTitle           the title screen, and where its start control is
 *   __projectJianghuCharacterCreate the character-creation step (placeholder in Stage 03)
 *   __projectJianghuBracket         the tournament-bracket step (placeholder in Stage 03)
 *   __projectJianghuRest            the rest house, and where its two controls are
 *   __projectJianghuArena           the arena, and where its two controls are
 *   __projectJianghuArenaSpike      the HD-2D spike: tier, frame rate, sorting probe
 *   __projectJianghuResult          the summary screen, and where its two controls are
 */
export const SMOKE = "__projectJianghuSmoke";
export const SCENE = "__projectJianghuScene";
export const THAI_SPIKE = "__projectJianghuThaiSpike";
export const TITLE = "__projectJianghuTitle";
export const CHARACTER_CREATE = "__projectJianghuCharacterCreate";
export const BRACKET = "__projectJianghuBracket";
export const REST = "__projectJianghuRest";
export const ARENA = "__projectJianghuArena";
export const ARENA_SPIKE = "__projectJianghuArenaSpike";
export const RESULT = "__projectJianghuResult";

/** How long a player may take to download, compile and boot. */
export const PLAYER_BOOT_TIMEOUT_MS = 180_000;

/** How long a scene handover (a fade plus a load) may take. */
export const HANDOVER_TIMEOUT_MS = 30_000;
