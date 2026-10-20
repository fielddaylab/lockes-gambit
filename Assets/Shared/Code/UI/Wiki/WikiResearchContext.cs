using FieldDay;
using SpaceFab.Research;

namespace SpaceFab.UI {
    /// <summary>
    /// The Research state the wiki's observation and property pages need to
    /// render interactively: which observations are already in the sample
    /// panel, which property is the active hypothesis, and which chamber is
    /// running. Absent in every scene but Research, where those pages fall
    /// back to a plain, inert render.
    /// </summary>
    public struct WikiResearchContext {
        // False when any of the three states is missing. Consumers treat a
        // partial context as no context rather than null-checking each field.
        public bool Present;

        public HypothesisViewModelState ViewModel;
        public ChamberInterfacerState InterfacerState;
        public ResearchMinigameState MinigameState;
    }

    /// <summary>
    /// Builder paired with WikiResearchContext. Resolve is called only at
    /// the wiki's two refresh seams — WikiRefreshSystem.DrainPendingWork and
    /// WikiState.OnSceneLateEnable — and the result is passed down as a
    /// parameter, so no wiki utility reaches into Research state on its own.
    /// </summary>
    public static class WikiResearchContextUtility {
        public static WikiResearchContext Resolve() {
            WikiResearchContext context = default;
            context.Present = Game.SharedState.TryGet(out context.ViewModel)
                && Game.SharedState.TryGet(out context.InterfacerState)
                && Game.SharedState.TryGet(out context.MinigameState);
            return context;
        }
    }
}
