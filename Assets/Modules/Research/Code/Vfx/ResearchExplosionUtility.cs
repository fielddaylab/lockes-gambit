using BeauRoutine;
using BeauRoutine.Extensions;
using FieldDay;
using FieldDay.Audio;
using FieldDay.Scripting;
using SpaceFab.Materials;
using System.Collections;
using UnityEngine;

namespace SpaceFab.Research {
    /// <summary>
    /// Visual style for a slot explosion. Each value picks a different
    /// preamble (e.g. bolt-zap for VoltageBreakdown) and shake animation.
    /// Today only Default and VoltageBreakdown have real routines; the rest
    /// fall through to Default until the chamber that fires them is
    /// migrated (TooBig / InvalidCombo for Combiner, TemperatureBreakdown
    /// variants for Thermal).
    /// </summary>
    public enum ExplosionStyle : byte {
        Default,
        VoltageBreakdown,
        TooBig,
        // InvalidCombo,
        TemperatureBreakdownHot,
        // TemperatureBreakdownCold,
        // Polyelemental,
        InvalidSubstrate
    }

    /// <summary>
    /// Entry point chamber systems call when a slot's held material fails a
    /// stability check. Starts a BeauRoutine on the slot that runs the
    /// style-specific animation, plays VFX and SFX, and finally clears the
    /// slot via ResearchSlotUtility.FillInSlot. Also flips
    /// ResearchExplosionState into its locked state and pauses input; the
    /// state's decay is driven by ResearchExplosionSystem.
    /// </summary>
    public static class ResearchExplosionUtility {
        // Requests an explosion on the given slot. No-op if the slot is null
        // or empty. The caller stops driving any per-slot visual side-effects
        // (e.g. the Battery zeroes its circuit) but does not clear the slot
        // itself — the routine does that at the end of the sequence.
        public static void ExplodeSlot(
            ResearchExplosionState explosionState,
            ResearchPools vfxPool,
            ChamberInterfacerState interfacerState,
            ResearchSlot slot,
            ChamberSlotKind kind,
            ExplosionStyle style,
            float delay = 0f
        ) {
            if (slot == null || slot.CurrentMaterial == null) {
                return;
            }

            slot.ExplosionRoutine.Replace(slot, ExplosionRoutine(vfxPool, interfacerState, slot, kind, style, delay));
            BeginExplosions(explosionState);
        }

        // The actual sequence. VoltageBreakdown leads with a bolt-zap flash;
        // every style then optionally waits, swaps the rig material for the
        // pre-explode flash, shakes the rig, plays the burst VFX + SFX, and
        // finally clears the slot via the standard FillInSlot path so the
        // chamber's frame-flag refires on the next frame.
        private static IEnumerator ExplosionRoutine(
            ResearchPools vfxPool,
            ChamberInterfacerState interfacerState,
            ResearchSlot slot,
            ChamberSlotKind kind,
            ExplosionStyle style,
            float delay
        ) {
            // 1. Style preamble. Today only VoltageBreakdown leads with a
            // bolt-zap VFX; other styles run the standard explosion below.
            if (style == ExplosionStyle.VoltageBreakdown) {
                if (vfxPool != null && vfxPool.BoltZapEffectPool != null && slot.Rig != null) {
                    ResearchVfxUtility.PlayFromPool(vfxPool.BoltZapEffectPool, slot.Rig.transform);
                }
            }

            if (delay > 0f) {
                yield return delay;
            }

            // 2. Pre-explode flash. Save the rig's original sharedMaterial so
            // the swap can be reverted at the end — the rig is pooled at the
            // slot level, so leaving the wrong material on it would bleed
            // into the next placed gem.
            Material originalMaterial = null;
            SpriteRenderer rigRenderer = slot.Rig != null ? slot.Rig.Renderer : null;
            if (rigRenderer != null && vfxPool != null && vfxPool.PreExplodeItemMaterial != null) {
                originalMaterial = rigRenderer.sharedMaterial;
                rigRenderer.sharedMaterial = vfxPool.PreExplodeItemMaterial;
            }

            // 3. Shake the rig in place. Wave returns the transform to its
            // starting position on completion, so no manual reset is needed.
            if (slot.Rig != null) {
                Transform rigT = slot.Rig.transform;
                float startY = rigT.localPosition.y;
                yield return rigT.MoveTo(startY + 0.1f, 0.27f, Axis.Y, Space.Self).Wave(Wave.Function.Sin, 6);
            }

            if (style == ExplosionStyle.InvalidSubstrate) {
                Vector2 originalPosition = slot.Rig.transform.position;
                KinematicAnimation animation = new() {
                    Velocity = new Vector3Range(new Vector3(-8f, 8f, 0f)),
                    Gravity = new Vector3Range(new Vector3(0f, -10f, 0f))
                };
                yield return Routine.Inline(animation.Simulate(slot.Rig.transform, 0.5f));
                slot.Rig.transform.position = originalPosition;
            }

            else {
                // 4. Burst VFX + SFX. The sfx key is shared across styles for
                // now; per-style audio will land when the audio bank does.
                if (vfxPool != null && vfxPool.ExplosionEffectPool != null && slot.Rig != null) {
                    ResearchVfxUtility.PlayFromPool(vfxPool.ExplosionEffectPool, slot.Rig.transform);
                }
                Sfx.Play("Research.Gem.Explode");

                yield return 0.05f;
            }

            // 5. Restore material then clear the slot. FillInSlot null fires
            // the standard frame-flag, which the owning chamber picks up
            // next frame to drop its visuals to the empty-slot state.
            if (rigRenderer != null && originalMaterial != null) {
                rigRenderer.sharedMaterial = originalMaterial;
            }
            ResearchSlotUtility.ClearSlot(interfacerState, slot, kind);
            
            using (var table = TempVarTable.Alloc())
            {
                table.Set("explosionStyle", style.ToString());
                ScriptUtility.Trigger(ResearchScriptTriggers.OnMaterialExploded, table);
            }
        }

        // Flips the shared explosion flag and pauses input. Idempotent — a
        // second BeginExplosions during an already-running explosion is a
        // no-op so PauseAll is not stacked.
        public static void BeginExplosions(ResearchExplosionState explosionState) {
            if (explosionState.AreAnyExploding) {
                return;
            }
            explosionState.AreAnyExploding = true;
            explosionState.StateTimer = explosionState.PreExplosionCooldown;
            Game.Input.PauseAll();
        }
    }
}
