using System;
using System.Collections.ObjectModel;
using BeauRoutine;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Animation;
using FieldDay.Scenes;
using FieldDay.SharedState;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.UI {
    /// <summary>
    /// Scene-authored layout references for the shared wiki UI: the panel root, the paginator
    /// strip and its scroll geometry, the selection highlight, and the widget set page content
    /// binds into.
    ///
    /// Authored once on the wiki prefab root alongside WikiContent and WikiPools, and written by
    /// WikiVisualsUtility when wiki state changes rather than on a per-frame poll.
    /// </summary>
    public class WikiLayoutState : SharedStateComponent {
        public Canvas RootCanvas;
        public RectTransform ExpandedRoot;
        public CanvasInputLayer InputLayer;
        public LayoutOffset Offset;

        public TextMeshProUGUI Header;

        public WikiTabinator Tabinator;
        public WikiPaginator Paginator;
        public WikiPageLayout PageLayout;
        public GuiButton CloseButton;

        [NonSerialized] public AnimHandle ExpandAnim;

        private void Awake() {
            CloseButton.OnClick.Register(OnClickClose);
            RootCanvas.enabled = false;
        }

        static private void OnClickClose() {
            WikiUtility.Close(Find.State<WikiViewState>());
        }
    }

    public static partial class WikiLayoutUtility {
        static public void SnapExpandedState(WikiLayoutState layout, bool expanded) {
            layout.RootCanvas.enabled = expanded;
            SetInputEnabled(layout, expanded);

            Anims.Cancel(ref layout.ExpandAnim);
            layout.Offset.Offset0 = new Vector2(expanded ? 0 : 370, 0);
        }

        static public void SetInputEnabled(WikiLayoutState layout, bool enabled) {
            layout.InputLayer.SetInputOverride(enabled ? null : false);
        }
    }
}
