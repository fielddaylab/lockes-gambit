using System;
using BeauUtil;
using FieldDay;
using FieldDay.Assets;
using UnityEngine;

namespace SpaceFab.Fabrication.Sequence
{
    /// <summary>
    /// ScriptableObject definition for a fabrication sequence: ordered steps and glitch authoring.
    /// Referenced by ContractAssetsWrapper. Card visuals are looked up at runtime via the global
    /// SequenceLookup asset (not authored per-level). The checkpoint-rollback lead-in is game-wide
    /// and lives on SequenceUtility.
    /// </summary>
    [CreateAssetMenu(menuName = "SpaceFab/Fabrication/Level")]
    public class FabricationLevel : NamedAsset, IRegistrationCallbacks
    {
        [NonSerialized] private FabricationSequence m_CachedSequenceRef;

        [SerializeField, AssetName(typeof(FabricationSequence))] private StringHash32 m_sequenceId;
        public FabricationSequence Sequence {
            get {
                if (m_CachedSequenceRef == null) {
                    m_CachedSequenceRef = Find.NamedAsset<FabricationSequence>(m_sequenceId);
                }
                return m_CachedSequenceRef;
            }
        }

        // Each step is independently rolled against this
        // probability at sequence reset. Range [0,1].
        [SerializeField, Range(0f, 1f)] private float m_GlitchChance = 0f;
        public float GlitchChance => m_GlitchChance;

        [SerializeField] private int m_PatternIndex;
        public int PatternIndex => m_PatternIndex;

        public int GetPatternIndex(SequenceState sequence)
        {
            int currStep = sequence.CurrentStepIndex;
            SequenceChunk chunk = sequence.Level.Sequence.Steps[currStep].Chunk;

            switch (chunk)
            {
                case SequenceChunk.N:
                    return 1;
                case SequenceChunk.P:
                    return 2;
                default:
                    return 0;
            }
        }

        void IRegistrationCallbacks.OnRegister() {
        }

        void IRegistrationCallbacks.OnDeregister() {
            m_CachedSequenceRef = null;
        }

        public void ClearCachedReferences() {
            m_CachedSequenceRef = null;
        }
    }
}