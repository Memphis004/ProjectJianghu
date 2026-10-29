/**
 * The shapes of the documents the player publishes on `window`, as the specs read them.
 *
 * These live in a `.ts` file rather than in `globals.mjs` on purpose: `globals.mjs` is imported by
 * plain node (`scripts/probe-boot.mjs`), so it must stay JavaScript — a TypeScript interface in it is
 * a parse error under that loader. The names themselves stay in `globals.mjs`, which remains the one
 * place a page global is written down.
 *
 * The fields mirror the C# report classes exactly (`TitleReport`, `StepReport`, `RestReport`,
 * `ArenaReport`, `ArenaSpikeReport`, `ResultReport`); a field added there and not here is a spec
 * that silently stops asserting on it.
 */

/** A control the screen published a point for. `x`/`y` are canvas pixels, origin bottom-left. */
export interface Control {
  name: string;
  x: number;
  y: number;
}

/** The part of every screen report a spec may rely on. */
export interface ScreenReport {
  scene: string;
  canvasWidth: number;
  canvasHeight: number;
  buttons: Control[];
}

/** `__projectJianghuTitle`. */
export interface TitleReport extends ScreenReport {
  startError: string;
  title: string;
  startLabel: string;
  diagnostics: string;
  ready: boolean;
}

/** `__projectJianghuCharacterCreate` and `__projectJianghuBracket`. */
export interface StepReport extends ScreenReport {
  title: string;
  line: string;
  continueLabel: string;
}

/** `__projectJianghuRest`. */
export interface RestReport extends ScreenReport {
  title: string;
  instructions: string;
  roundsLine: string;
  departLabel: string;
  finishLabel: string;
}

/** `__projectJianghuArena`. */
export interface ArenaReport extends ScreenReport {
  title: string;
  progressLine: string;
  winLabel: string;
  loseLabel: string;
}

/** `__projectJianghuArenaSpike`: the HD-2D spike's measurement, read by the tier probe. */
export interface ArenaSpikeReport {
  scene: string;
  quality: { tier: string; level: number; pipelineAssigned: boolean; renderScale: number; msaa: number; tiers: string[] };
  sample: {
    frames: number;
    seconds: number;
    fps: number;
    onePercentLowFps: number;
    worstMillis: number;
    canvasWidth: number;
    canvasHeight: number;
  };
  sorting: {
    measured: boolean;
    spriteInFrontDrawsOverProp?: boolean;
    propOccludesSpriteBehind?: boolean;
    nearerSpriteDrawsFirst?: boolean;
    authoredOrderDecides?: boolean;
    passed?: boolean;
  };
  camera: { orthographic: boolean; yaw: number; pitch: number; pixelsPerUnit: number; zoom: number; orthographicSize: number };
  pixelSnap: { pixelsPerUnit: number; maxOffsetPixels: number };
  spriteCount: number;
  propCount: number;
}

/** `__projectJianghuResult`. */
export interface ResultReport extends ScreenReport {
  startError: string;
  outcome: string;
  finished: boolean;
  encounters: number;
  seed: string;
  headline: string;
  summaryLine: string;
  seedLine: string;
  restartLabel: string;
  titleLabel: string;
}
