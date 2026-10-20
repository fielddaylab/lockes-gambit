using BeauUtil;
using BeauUtil.Debugger;
using BeauUtil.UI;
using BeauUtil.Variants;
using System;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FieldDay.UI.Widgets {
    public sealed class GuiButton : GuiWidget, IGuiDataWidget {
        public abstract class Style : MonoBehaviour, IGuiWidgetStyle, IGuiWidgetInteractiveStyle {
            public GuiButton Widget { get; private set; }

            public virtual void Bind(GuiWidget source) {
                Widget = (GuiButton) source;
            }

            public abstract void OnClick(GuiButton button);
            public abstract void UpdateInteractionState(GuiWidgetInteractableState state, GuiWidget source, GuiWidgetUpdateFlags flags);
            public virtual void UpdateState(GuiWidgetStateFlags state, GuiWidgetStateFlags changed, GuiWidget source, GuiWidgetUpdateFlags flags) { }
        }

        [SerializeField] private Style m_Style;

        [NonSerialized] private GuiWidgetInteractableState m_InteractableState;
        [NonSerialized] private bool m_WasClicked = false;
        [NonSerialized] private Variant m_DataVariant;

        public PointerListener.PointerEvent OnClick {
            get { return CursorHint.onClick; }
        }

        public bool ConsumeClick() {
            bool former = m_WasClicked;
            m_WasClicked = false;
            return former;
        }

        protected override void Awake() {
            base.Awake();

            if (m_Style) {
                AssignBaseStyle(m_Style);

                CursorHint.onPointerDown.Register(HandleCursorEvent);
                CursorHint.onPointerUp.Register(HandleCursorEvent);
                CursorHint.onPointerEnter.Register(HandleCursorEvent);
                CursorHint.onPointerExit.Register(HandleCursorEvent);
            }
            
            CursorHint.onClick.Register(HandleCursorClick);

            TryUpdateInteractableState(this, ref m_InteractableState, m_Style, GuiWidgetUpdateFlags.Initialization);
        }

        protected override void OnDisable() {
            m_WasClicked = false;
            base.OnDisable();
        }

        protected override void UpdateState(GuiWidgetStateFlags state, GuiWidgetStateFlags change, GuiWidgetUpdateFlags flags = GuiWidgetUpdateFlags.Default) {
            CursorHint.enabled = (state & GuiWidgetStateFlags.PauseInteractions) == 0;
            if (CanvasGraphic) {
                CanvasGraphic.raycastTarget = (state & GuiWidgetStateFlags.PauseInteractions) == 0;
            }

            TryUpdateInteractableState(this, ref m_InteractableState, m_Style);
        }

        #region Toggle

        public bool GetToggleState() {
            return (State & GuiWidgetStateFlags.IsToggleOn) != 0;
        }

        public bool SetToggleState(bool toggleState, GuiWidgetUpdateFlags flags = GuiWidgetUpdateFlags.Default) {
            return TryUpdateState(this, Bits.Set(State, GuiWidgetStateFlags.IsToggleOn, toggleState), flags);
        }

        #endregion // Toggle

        #region IGuiDataWidget

        public Variant GetVariantValue() {
            return m_DataVariant;
        }

        public void SetVariantValue(Variant variant, GuiWidgetUpdateFlags flags = GuiWidgetUpdateFlags.Default) {
            m_DataVariant = variant;
        }

        public void ResetValue(GuiWidgetUpdateFlags flags = GuiWidgetUpdateFlags.Default) {
            m_DataVariant = default;
        }

        #endregion // IGuiDataWidget

        static private readonly Action<PointerListener.EventData> HandleCursorClick = (data) => {
            GuiButton button = Resolve(data);
            button.m_WasClicked = true;
            if (button.m_Style) {
                button.m_Style.OnClick(button);
            }
        };

        static private readonly Action<PointerListener.EventData> HandleCursorEvent = (data) => {
            GuiButton button = Resolve(data);
            TryUpdateInteractableState(button, ref button.m_InteractableState, button.m_Style);
        };

        /// <summary>
        /// Retrieves the GuiButton referenced by the given event data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public GuiButton Resolve(PointerListener.EventData data) {
            Assert.True(data.Source is CursorHint, "Pointer data did not originate from a CursorHint");
            CursorHint cursor = Unsafe.FastCast<CursorHint>(data.Source);

            Assert.True(cursor.Owner is GuiButton, "Cursor hint is not owned by a GuiButton");
            GuiButton button = Unsafe.FastCast<GuiButton>(cursor.Owner);

            return button;
        }

        /// <summary>
        /// Retrieves the value assigned to the GuiButton referenced by the given event data.
        /// </summary>
        static public Variant GetData(PointerListener.EventData data) {
            GuiButton button = Resolve(data);
            return button.GetVariantValue();
        }
    }
}