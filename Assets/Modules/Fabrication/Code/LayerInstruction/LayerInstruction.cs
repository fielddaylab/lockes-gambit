using FieldDay.Assets;
using SpaceFab.Fabrication.Sequence;
using System;
using UnityEngine;

namespace SpaceFab.Fabrication.LayerInstructions
{
    /// <summary>
    /// Inspector entry for one layer's row visuals.
    /// </summary>
    [Serializable]
    public struct LayerVisualEntry
    {
        public SequenceChunk Layer;
        public Sprite Icon;
        public Sprite BG;
    }

    [CreateAssetMenu(menuName = "SpaceFab/Fabrication/Layer Instruction")]
    public class LayerInstruction : NamedAsset
    {
        [SerializeField] private LayerVisualEntry[] m_layerVisuals;
        public LayerVisualEntry[] LayerVisuals => m_layerVisuals;
    }
}
