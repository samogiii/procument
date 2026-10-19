<template>
  <Transition name="robot-pop">
    <div v-if="show" class="robot-dock" role="status" aria-live="polite">
      <div v-if="message" class="robot-bubble" :class="{ 'robot-bubble--error': error }">
        {{ message }}
        <button class="robot-bubble__close" aria-label="Dismiss" @click="emit('close')">×</button>
      </div>
      <div class="robot-stage" :class="{ 'is-dancing': dancing, 'is-sad': error }" aria-hidden="true">
        <div class="robot-tilt">
          <div class="robot">
            <!-- Each part is a small CSS cuboid: six faces folded in 3D, no images or WebGL -->
            <div v-for="part in parts" :key="part.name" :class="['robot-part', `robot-${part.name}`]" :style="part.style">
              <div v-for="face in faces" :key="face" :class="['robot-face', `robot-face--${face}`]" />
            </div>
          </div>
        </div>
        <div class="robot-shadow" />
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
/**
 * A tiny CSS-3D robot that dances while something runs in the background (e.g. MailFlow
 * reading quote emails). Place it inside a `position: relative` wrapper around the button
 * it belongs to: it floats just left of that wrapper, its speech bubble below — neither
 * takes up layout space. Pure CSS: ~50 divs, transform-only animations, still for
 * prefers-reduced-motion.
 */
defineProps<{
  show: boolean
  /** true = dance; false = stand still (finished) */
  dancing?: boolean
  /** Speech bubble text */
  message?: string
  /** Sad robot + red bubble */
  error?: boolean
}>()
const emit = defineEmits<{ close: [] }>()

const faces = ['front', 'back', 'right', 'left', 'top', 'bottom'] as const

// Sizes in px: width × height × depth, and where the part's top-left sits in the 70×92 robot box.
function box(name: string, w: number, h: number, d: number, x: number, y: number) {
  return { name, style: { '--w': `${w}px`, '--h': `${h}px`, '--d': `${d}px`, left: `${x}px`, top: `${y}px` } }
}
const parts = [
  box('antenna', 3, 10, 3, 33.5, 0),
  box('head', 30, 24, 24, 20, 8),
  box('body', 34, 30, 22, 18, 34),
  box('arm-l', 9, 26, 9, 8, 36),
  box('arm-r', 9, 26, 9, 53, 36),
  box('leg-l', 11, 24, 11, 22, 64),
  box('leg-r', 11, 24, 11, 37, 64),
]
</script>

<style scoped>
/* Just left of the button, vertically centred on it; the 70×100 stage is drawn at 60%. */
.robot-dock {
  --robot-scale: 0.6;
  position: absolute;
  right: calc(100% + 4px);
  top: 50%;
  width: calc(70px * var(--robot-scale));
  height: calc(100px * var(--robot-scale));
  margin-top: calc(-50px * var(--robot-scale));
  z-index: 5;
  pointer-events: none;
  transform-origin: right center;
}

/* Hangs below the robot, right edge under it, tail pointing up at it. */
.robot-bubble {
  pointer-events: auto;
  position: absolute;
  top: calc(100% + 8px);
  right: 0;
  width: max-content;
  max-width: 240px;
  padding: 8px 26px 8px 12px;
  border-radius: 12px;
  background: rgb(var(--v-theme-surface));
  color: rgb(var(--v-theme-on-surface));
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.15);
  font-size: 0.8rem;
  line-height: 1.35;
}
.robot-bubble::after {
  content: '';
  position: absolute;
  right: 16px;
  top: -6px;
  width: 10px;
  height: 10px;
  background: inherit;
  border-left: inherit;
  border-top: inherit;
  rotate: 45deg;
}
.robot-bubble--error { border-color: rgb(var(--v-theme-error)); }
.robot-bubble__close {
  position: absolute;
  top: 2px;
  right: 6px;
  border: 0;
  background: none;
  color: inherit;
  opacity: 0.6;
  font-size: 1rem;
  cursor: pointer;
}

/* ── Stage ─────────────────────────────────────────────── */
.robot-stage {
  --metal: rgb(var(--v-theme-primary));
  --shell: #eceff4;
  width: 70px;
  height: 100px;
  position: relative;
  perspective: 420px;
  scale: var(--robot-scale);
  transform-origin: 0 0;
}
.robot-tilt {
  position: absolute;
  inset: 0;
  transform-style: preserve-3d;
  transform: rotateX(-12deg);
}
.robot {
  position: absolute;
  inset: 0;
  transform-style: preserve-3d;
  /* identity values: the animations below only change these */
  translate: 0 0;
  rotate: y 0deg;
}
.robot-shadow {
  position: absolute;
  left: 14px;
  right: 14px;
  bottom: 2px;
  height: 7px;
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.22);
  filter: blur(2px);
  scale: 1;
}

/* ── Cuboid ────────────────────────────────────────────── */
.robot-part {
  position: absolute;
  width: var(--w);
  height: var(--h);
  transform-style: preserve-3d;
  rotate: x 0deg;
  --c: var(--metal);
}
.robot-face {
  position: absolute;
  width: var(--w);
  height: var(--h);
  background: var(--c);
  backface-visibility: hidden;
}
.robot-face--front { transform: translateZ(calc(var(--d) / 2)); }
.robot-face--back { transform: rotateY(180deg) translateZ(calc(var(--d) / 2)); }
.robot-face--right,
.robot-face--left {
  width: var(--d);
  left: calc((var(--w) - var(--d)) / 2);
  background: color-mix(in srgb, var(--c) 78%, black);
}
.robot-face--right { transform: rotateY(90deg) translateZ(calc(var(--w) / 2)); }
.robot-face--left { transform: rotateY(-90deg) translateZ(calc(var(--w) / 2)); }
.robot-face--top,
.robot-face--bottom {
  height: var(--d);
  top: calc((var(--h) - var(--d)) / 2);
  background: color-mix(in srgb, var(--c) 72%, white);
}
.robot-face--top { transform: rotateX(90deg) translateZ(calc(var(--h) / 2)); }
.robot-face--bottom { transform: rotateX(-90deg) translateZ(calc(var(--h) / 2)); }

/* ── Parts ─────────────────────────────────────────────── */
.robot-head { --c: var(--shell); }
/* visor with two glowing eyes */
.robot-head .robot-face--front {
  background:
    radial-gradient(circle at 32% 50%, #5ff 0 2.6px, transparent 3.2px),
    radial-gradient(circle at 68% 50%, #5ff 0 2.6px, transparent 3.2px),
    linear-gradient(#1d2433, #1d2433) center / 80% 46% no-repeat,
    var(--shell);
}
.robot-antenna { --c: #9aa3b2; }
.robot-antenna .robot-face--front::before {
  content: '';
  position: absolute;
  left: -3px;
  top: -6px;
  width: 9px;
  height: 9px;
  border-radius: 50%;
  background: rgb(var(--v-theme-warning));
  box-shadow: 0 0 6px rgb(var(--v-theme-warning));
}
/* chest light */
.robot-body .robot-face--front {
  background:
    radial-gradient(circle at 50% 38%, rgb(var(--v-theme-warning)) 0 3px, transparent 3.6px),
    linear-gradient(color-mix(in srgb, var(--metal) 70%, white), color-mix(in srgb, var(--metal) 70%, white)) 50% 75% / 60% 3px no-repeat,
    var(--metal);
}
.robot-arm-l, .robot-arm-r { --c: var(--shell); transform-origin: 50% 3px; rotate: z 0deg; }
.robot-leg-l, .robot-leg-r { --c: #5b6577; transform-origin: 50% 0; }

/* ── Dance ─────────────────────────────────────────────── */
.is-dancing .robot { animation: robot-sway 1.6s ease-in-out infinite, robot-bounce 0.4s ease-in-out infinite; }
.is-dancing .robot-head { animation: robot-nod 0.8s ease-in-out infinite; transform-origin: 50% 100%; }
.is-dancing .robot-arm-l { animation: robot-wave-l 0.8s ease-in-out infinite; }
.is-dancing .robot-arm-r { animation: robot-wave-r 0.8s ease-in-out infinite; }
.is-dancing .robot-leg-l { animation: robot-step 0.8s ease-in-out infinite; }
.is-dancing .robot-leg-r { animation: robot-step 0.8s ease-in-out infinite -0.4s; }
.is-dancing .robot-shadow { animation: robot-shadow 0.4s ease-in-out infinite; }
.is-dancing .robot-antenna .robot-face--front::before { animation: robot-blink 0.8s steps(2) infinite; }

@keyframes robot-sway {
  0%, 100% { rotate: y -35deg; }
  50% { rotate: y 35deg; }
}
@keyframes robot-bounce {
  0%, 100% { translate: 0 0; }
  50% { translate: 0 -6px; }
}
@keyframes robot-nod {
  0%, 100% { rotate: z -8deg; }
  50% { rotate: z 8deg; }
}
@keyframes robot-wave-l {
  0%, 100% { rotate: z 20deg; }
  50% { rotate: z 150deg; }
}
@keyframes robot-wave-r {
  0%, 100% { rotate: z -150deg; }
  50% { rotate: z -20deg; }
}
@keyframes robot-step {
  0%, 100% { rotate: x 0deg; }
  50% { rotate: x 28deg; }
}
@keyframes robot-shadow {
  0%, 100% { scale: 1; }
  50% { scale: 0.8; }
}
@keyframes robot-blink {
  50% { opacity: 0.25; }
}

/* Sad: arms down, head low */
.is-sad .robot-head { rotate: x 18deg; transform-origin: 50% 100%; }
.is-sad .robot-antenna .robot-face--front::before { background: rgb(var(--v-theme-error)); box-shadow: 0 0 6px rgb(var(--v-theme-error)); }

/* ── Enter / leave ─────────────────────────────────────── */
.robot-pop-enter-active { transition: opacity 0.25s ease, translate 0.45s cubic-bezier(0.34, 1.56, 0.64, 1), scale 0.45s cubic-bezier(0.34, 1.56, 0.64, 1); }
.robot-pop-leave-active { transition: opacity 0.3s ease, translate 0.3s ease-in, scale 0.3s ease-in; }
.robot-pop-enter-from,
.robot-pop-leave-to { opacity: 0; translate: 16px 0; scale: 0.4; }
.robot-dock { translate: 0 0; scale: 1; }

@media (prefers-reduced-motion: reduce) {
  .robot-stage *,
  .robot-stage *::before { animation: none !important; }
  .robot-pop-enter-active,
  .robot-pop-leave-active { transition: opacity 0.2s ease; }
  .robot-pop-enter-from,
  .robot-pop-leave-to { translate: 0 0; scale: 1; }
}
</style>
