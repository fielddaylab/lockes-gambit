using BeauRoutine;
using FieldDay.Components;
using SpaceFab.Fabrication.Sequence;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication.LayerInstructions
{
    /// <summary>
    /// View component for the Layer Instruction Panel. Shows one row per layer authored on the
    /// sequence's Layer Instruction asset. Shown briefly when the lead-in countdown ends, and in
    /// place of the completion recap when a completed step moves the sequence to a new chunk.
    /// </summary>
    public class LayerInstructionPanel : BatchedComponent
    {
        public CanvasGroup Group;
        public LayerInstructionRow RowPrefab;
        public Transform Rows;

        public float FadeSeconds;
        public float HoldSeconds;

        // One row per Layer Visuals entry on the sequence's Layer Instruction asset.
        [NonSerialized] private readonly List<LayerInstructionRow> m_rows = new List<LayerInstructionRow>();
        [NonSerialized] private readonly List<SequenceChunk> m_rowChunks = new List<SequenceChunk>();

        // Show-and-hide played on Initialize (sequence start / reset / checkpoint restore).
        [NonSerialized] private Routine m_introRoutine;

        // Start hidden; the panel only appears via Show (countdown end) or ChunkChangeRoutine.
        private void Awake()
        {
            SetVisible(false);
        }

        // Rebuilds the rows for the given sequence and leaves the panel hidden; call Show to play
        // the show-and-hide (done when the lead-in countdown ends). currentStepIndex is non-zero
        // after a checkpoint restore; layers of steps before it are marked complete and the
        // current one active.
        public void Initialize(FabricationSequence sequence, int currentStepIndex)
        {
            m_introRoutine.Stop();
            SetVisible(false);
            for (int i = Rows.childCount - 1; i >= 0; i--) {
                Destroy(Rows.GetChild(i).gameObject);
            }
            m_rows.Clear();
            m_rowChunks.Clear();

            LayerVisualEntry[] layers = sequence != null && sequence.LayerInstruction != null ? sequence.LayerInstruction.LayerVisuals : null;
            if (layers == null || layers.Length == 0) {
                return;
            }

            // Rows come from the sequence's Layer Instruction asset, in the order authored there.
            for (int i = 0; i < layers.Length; i++) {
                if (!m_rowChunks.Contains(layers[i].Layer)) {
                    m_rowChunks.Add(layers[i].Layer);
                    m_rows.Add(CreateRow(layers[i]));
                }
            }

            FabricationStep[] steps = sequence.Steps;
            if (steps == null || currentStepIndex <= 0 || currentStepIndex >= steps.Length) {
                MarkActive(m_rowChunks[0]);
            } else {
                SequenceChunk current = steps[currentStepIndex].Chunk;
                for (int i = 0; i < currentStepIndex; i++) {
                    int row = m_rowChunks.IndexOf(steps[i].Chunk);
                    if (row >= 0 && steps[i].Chunk != current) {
                        m_rows[row].MarkRowComplete();
                    }
                }
                MarkActive(current);
            }
        }

        // Fades the panel in, holds, and fades it out. No-op if Initialize built no rows.
        public void Show()
        {
            if (m_rows.Count == 0) {
                return;
            }
            m_introRoutine.Replace(this, ShowAndHide());
        }

        // True if this panel has rows and the step after completedIndex is on a different chunk.
        // The final step returns false, so the sequence's last step still plays the normal recap.
        public bool IsChunkChange(FabricationStep[] steps, int completedIndex)
        {
            if (m_rows.Count == 0 || steps == null || completedIndex < 0 || completedIndex + 1 >= steps.Length) {
                return false;
            }
            return steps[completedIndex + 1].Chunk != steps[completedIndex].Chunk;
        }

        // Played by CompletionRecapSystem instead of the recap when IsChunkChange is true. Marks
        // the finished chunk complete and the next one active, then shows and hides the panel.
        public IEnumerator ChunkChangeRoutine(FabricationStep[] steps, int completedIndex)
        {
            m_introRoutine.Stop();

            int finishedRow = m_rowChunks.IndexOf(steps[completedIndex].Chunk);
            if (finishedRow >= 0) {
                m_rows[finishedRow].MarkRowComplete();
            }
            MarkActive(steps[completedIndex + 1].Chunk);

            yield return ShowAndHide();
        }

        public void SetVisible(bool visible)
        {
            Group.alpha = visible ? 1f : 0f;
            Group.interactable = visible;
            Group.blocksRaycasts = visible;
        }

        private IEnumerator ShowAndHide()
        {
            SetVisible(true);
            Group.alpha = 0f;
            yield return Group.FadeTo(1f, FadeSeconds);
            yield return HoldSeconds;
            yield return Group.FadeTo(0f, FadeSeconds);
            SetVisible(false);
        }

        private void MarkActive(SequenceChunk chunk)
        {
            int row = m_rowChunks.IndexOf(chunk);
            if (row >= 0) {
                m_rows[row].MarkRowActive();
            }
        }

        private LayerInstructionRow CreateRow(LayerVisualEntry entry)
        {
            LayerInstructionRow row = Instantiate(RowPrefab, Rows);
            row.Initialize();
            switch (entry.Layer)
            {
                case SequenceChunk.N:
                    row.LayerText.text = "N-Type Layer";
                    break;
                case SequenceChunk.P:
                    row.LayerText.text = "P-Type Layer";
                    break;
                case SequenceChunk.Metal:
                    row.LayerText.text = "Metal Layer";
                    break;
            }

            row.LayerIcon.sprite = entry.Icon;
            row.RowBG.sprite = entry.BG;
            return row;
        }
    }
}
