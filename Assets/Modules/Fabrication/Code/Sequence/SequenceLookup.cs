using BeauUtil;
using FieldDay.Assets;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication.Sequence
{
    /// <summary>
    /// The three valid sequence-chunk categories, each corresponding to a layer of the wafer being
    /// fabricated. A chunk determines the background visuals/text shown on a step's hint card.
    /// </summary>
    public enum SequenceChunk
    {
        N,      // N-type doping chunk.
        P,      // P-type doping chunk.
        Metal   // Metal deposition chunk.
    }

    /// <summary>
    /// The full finite set of sequence steps. Each value maps 1:1 to a station (resolved via
    /// SequenceLookup.GetStationForStep) and to a composed card visual (ConvertFrom / ConvertToA /
    /// optional ConvertToB layered overlay, station icon, station + instruction labels).
    /// </summary>
    public enum SequenceStepID
    {
        AddStencil_Oxide,
        ApplyResist,
        DrawPattern,
        EtchPattern,
        FillStencil_Sputter,
        FillStencil_Dope,
    }

    /// <summary>
    /// Card-display data for a single sequence step. Looked up from SequenceLookup by step id at
    /// runtime. The card's wafer images compose from a separate WaferStepUILookup: one
    /// ConvertFrom id, one required ConvertToA id, and an optional ConvertToB overlay id.
    /// </summary>
    [Serializable]
    public struct SequenceStepEntry
    {
        // Which step this entry describes. Matches FabricationStep.StepId authored on a level asset.
        public SequenceStepID StepId;

        // The station the player must visit for this step. Authored as SerializedHash32 so it can
        // be set from the inspector; matches MicrogameStationInterfacer.Id at runtime.
        public SerializedHash32 StationId;

        // "Convert from" wafer image — what the wafer looks like at the start of this step.
        // Resolved against WaferStepUILookup at runtime; rendered on SequenceCard.WaferState1.
        [WaferStepUIRef] public SerializedHash32 ConvertFrom;

        // "Convert to" wafer image (base) — what the wafer looks like after this step. Required.
        // Resolved against WaferStepUILookup; rendered on SequenceCard.WaferState2Base.
        [WaferStepUIRef] public SerializedHash32 ConvertToA;

        // Optional second "convert to" image layered on top of ConvertToA (e.g., an oxide layer
        // on top of the base wafer). Resolved against WaferStepUILookup; left empty when no overlay
        // is needed — in that case SequenceCard.WaferState2Overlay is disabled at populate time.
        [WaferStepUIRef] public SerializedHash32 ConvertToB;

        // Sprite shown on SequenceCard.StationIcon for this step.
        public Sprite StationIconSprite;

        // Label shown on SequenceCard.StationLabelText (e.g., the station's display name).
        public string StationLabel;

        // Short imperative shown on SequenceCard.InstructionLabelText (e.g., "Apply resist").
        public string InstructionLabel;
    }

    /// <summary>
    /// Card-display data for a sequence chunk (N / P / Metal). Looked up from SequenceLookup by
    /// chunk at runtime to render the step's background (chunk-specific) visuals and text.
    /// </summary>
    [Serializable]
    public struct SequenceChunkEntry
    {
        public SequenceChunk Chunk;
        public Sprite ChunkSprite;
        public Sprite ChunkSequenceBG;
        public Sprite ChunkSequenceHeaderBG;
        public string ChunkText;
    }

    /// <summary>
    /// Global asset holding all card-display lookups for the Fabrication sequence feature:
    /// per-step foreground (composed wafer images, station icon + labels, station mapping),
    /// per-chunk background (sprite + text), and the shared glitch overlay applied when a step's
    /// card is glitched. Accessed via Find.GlobalAsset&lt;SequenceLookup&gt;().
    /// </summary>
    [CreateAssetMenu(menuName = "SpaceFab/Fabrication/Sequence Lookup")]
    public class SequenceLookup : GlobalAsset
    {
        [SerializeField] private SequenceStepEntry[] m_Steps;
        [SerializeField] private SequenceChunkEntry[] m_Chunks;

        // Overlay visuals applied to any step whose card is glitched. One overlay for all glitched
        // steps; covers everything (station name, description, visuals).
        [SerializeField] private Sprite m_GlitchOverlaySprite;
        [SerializeField] private string m_GlitchOverlayText;

        // Cached lookups, built lazily on first access.
        private Dictionary<SequenceStepID, SequenceStepEntry> m_StepsByIdCache;
        private Dictionary<SequenceChunk, SequenceChunkEntry> m_ChunksByKeyCache;

        public Sprite GlitchOverlaySprite => m_GlitchOverlaySprite;
        public string GlitchOverlayText => m_GlitchOverlayText;

        // Returns the step entry for the given step id, or the default (zeroed) entry if missing.
        public SequenceStepEntry GetStep(SequenceStepID stepId)
        {
            EnsureCaches();
            m_StepsByIdCache.TryGetValue(stepId, out SequenceStepEntry entry);
            return entry;
        }

        // Returns the station id for the given step. Convenience over GetStep(stepId).StationId.
        public SerializedHash32 GetStationForStep(SequenceStepID stepId)
        {
            return GetStep(stepId).StationId;
        }

        // Returns the chunk entry for the given chunk, or the default entry if missing.
        public SequenceChunkEntry GetChunk(SequenceChunk chunk)
        {
            EnsureCaches();
            m_ChunksByKeyCache.TryGetValue(chunk, out SequenceChunkEntry entry);
            return entry;
        }

        // Builds the per-step and per-chunk dictionaries on first lookup. Idempotent.
        private void EnsureCaches()
        {
            if (m_StepsByIdCache != null && m_ChunksByKeyCache != null) {
                return;
            }
            int stepCount = m_Steps != null ? m_Steps.Length : 0;
            m_StepsByIdCache = new Dictionary<SequenceStepID, SequenceStepEntry>(stepCount);
            for (int i = 0; i < stepCount; i++) {
                m_StepsByIdCache[m_Steps[i].StepId] = m_Steps[i];
            }
            int chunkCount = m_Chunks != null ? m_Chunks.Length : 0;
            m_ChunksByKeyCache = new Dictionary<SequenceChunk, SequenceChunkEntry>(chunkCount);
            for (int i = 0; i < chunkCount; i++) {
                m_ChunksByKeyCache[m_Chunks[i].Chunk] = m_Chunks[i];
            }
        }
    }
}
