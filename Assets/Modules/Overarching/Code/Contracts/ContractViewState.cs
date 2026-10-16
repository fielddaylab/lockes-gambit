using System;
using System.Collections;
using FieldDay.SharedState;

namespace SpaceFab.Overarching {
    public sealed class ContractViewState : SharedStateComponent {
        [NonSerialized] public int ViewingContractIndex = -1;
        [NonSerialized] public int QueuedContractViewIndex = -1;

        [NonSerialized] public OverarchingContractMode CurrentMode;
    }

    static public partial class ContractSequenceUtility {
        
    }
}