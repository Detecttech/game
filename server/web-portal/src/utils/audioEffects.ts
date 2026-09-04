// Web Audio API synthesizer for live match spectator sound effects
// Zero external assets needed, high-fidelity responsive audio.

let audioCtx: AudioContext | null = null;
let muted = false;

try {
  muted = localStorage.getItem("qb_teacher_sfx_muted") === "true";
} catch {
  // ignore
}

export function isAudioMuted(): boolean {
  return muted;
}

export function setAudioMuted(val: boolean): void {
  muted = val;
  try {
    localStorage.setItem("qb_teacher_sfx_muted", String(val));
  } catch {
    // ignore
  }
}

function getAudioContext(): AudioContext | null {
  if (muted) return null;
  if (!audioCtx) {
    const AudioContextClass = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
    if (AudioContextClass) {
      audioCtx = new AudioContextClass();
    }
  }
  if (audioCtx && audioCtx.state === "suspended") {
    audioCtx.resume().catch(() => {});
  }
  return audioCtx;
}

export function playAdvanceSound(): void {
  const ctx = getAudioContext();
  if (!ctx) return;
  const now = ctx.currentTime;

  const osc = ctx.createOscillator();
  const gain = ctx.createGain();

  osc.type = "sine";
  osc.frequency.setValueAtTime(440, now);
  osc.frequency.exponentialRampToValueAtTime(660, now + 0.08);

  gain.gain.setValueAtTime(0.12, now);
  gain.gain.exponentialRampToValueAtTime(0.001, now + 0.12);

  osc.connect(gain);
  gain.connect(ctx.destination);

  osc.start(now);
  osc.stop(now + 0.12);
}

export function playAttackSound(): void {
  const ctx = getAudioContext();
  if (!ctx) return;
  const now = ctx.currentTime;

  // Attack whoosh / zap
  const osc = ctx.createOscillator();
  const gain = ctx.createGain();

  osc.type = "sawtooth";
  osc.frequency.setValueAtTime(520, now);
  osc.frequency.exponentialRampToValueAtTime(140, now + 0.15);

  gain.gain.setValueAtTime(0.15, now);
  gain.gain.exponentialRampToValueAtTime(0.001, now + 0.18);

  osc.connect(gain);
  gain.connect(ctx.destination);

  osc.start(now);
  osc.stop(now + 0.18);

  // Thud hit shortly after
  setTimeout(() => {
    const ctxHit = getAudioContext();
    if (!ctxHit) return;
    const t = ctxHit.currentTime;
    const hitOsc = ctxHit.createOscillator();
    const hitGain = ctxHit.createGain();

    hitOsc.type = "triangle";
    hitOsc.frequency.setValueAtTime(120, t);
    hitOsc.frequency.exponentialRampToValueAtTime(40, t + 0.12);

    hitGain.gain.setValueAtTime(0.2, t);
    hitGain.gain.exponentialRampToValueAtTime(0.001, t + 0.12);

    hitOsc.connect(hitGain);
    hitGain.connect(ctxHit.destination);

    hitOsc.start(t);
    hitOsc.stop(t + 0.12);
  }, 120);
}

export function playFreezeSound(): void {
  const ctx = getAudioContext();
  if (!ctx) return;
  const now = ctx.currentTime;

  // Shimmering crystal chime
  [880, 1174, 1567, 1760].forEach((freq, i) => {
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    const startTime = now + i * 0.04;

    osc.type = "sine";
    osc.frequency.setValueAtTime(freq, startTime);

    gain.gain.setValueAtTime(0.08, startTime);
    gain.gain.exponentialRampToValueAtTime(0.001, startTime + 0.25);

    osc.connect(gain);
    gain.connect(ctx.destination);

    osc.start(startTime);
    osc.stop(startTime + 0.25);
  });
}

export function playVictorySound(): void {
  const ctx = getAudioContext();
  if (!ctx) return;
  const now = ctx.currentTime;

  // Major fanfare: C5, E5, G5, C6
  const notes = [523.25, 659.25, 783.99, 1046.5];
  notes.forEach((freq, idx) => {
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    const t = now + idx * 0.12;

    osc.type = "triangle";
    osc.frequency.setValueAtTime(freq, t);

    gain.gain.setValueAtTime(0.18, t);
    gain.gain.exponentialRampToValueAtTime(0.001, t + 0.35);

    osc.connect(gain);
    gain.connect(ctx.destination);

    osc.start(t);
    osc.stop(t + 0.35);
  });
}
