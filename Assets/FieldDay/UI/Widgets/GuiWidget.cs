using BeauUtil;
using BeauUtil.Debugger;
using BeauUtil.UI;
using BeauUtil.Variants;
using FieldDay.Components;
using System;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FieldDay.UI.Widgets {
    [RequireComponent(typeof(RectTransform))]
    public abstract class GuiWidget : BatchedComponent {
        [NonSerialized] private RectTransform m_RectTransform;
        [NonSerialized] private IGuiPanel m_Panel;
        [NonSerialized] private LayoutOffset m_LayoutOffset;
        [NonSerialized] private LayoutSizeGroup m_LayoutGroup;
        [NonSerialized] private IColorPaletteTint m_Tint;
        [NonSerialized] protected CanvasGroup m_CanvasGroup;

        [NonSerialized] private GuiWidgetStateFlags m_StateFlags;
        [NonSerialized] private IGuiWidgetStyle m_BaseStyle;

        [SerializeField] private SerializedHash32 m_Id;
        [SerializeField] private SerializedHash32 m_Class;
        [SerializeField] private SerializedHash32 m_Group;

        [Header("Components")]
        [SerializeField] private CursorHint m_Cursor;
        [SerializeField] private Graphic m_PrimaryGraphic;

        #region Identifiers

        /// <summary>
        /// The identifier for this widget.
        /// </summary>
        public StringHash32 Id {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_Id; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { m_Id = value; }
        }

        /// <summary>
        /// The class identifier for this widget.
        /// </summary>
        public StringHash32 Class {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_Class; }
        }

        /// <summary>
        /// The group identifier for this widget.
        /// </summary>
        public StringHash32 Group {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_Group; }
        }

        #endregion // Identifiers

        #region References

        /// <summary>
        /// The RectTransform.
        /// </summary>
        public RectTransform Rect {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ReferenceEquals(m_RectTransform, null) ? (m_RectTransform = (RectTransform)transform) : m_RectTransform; }
        }

        /// <summary>
        /// The closest parent GuiPanel.
        /// </summary>
        public IGuiPanel Panel {
            get { return ReferenceEquals(m_Panel, null) ? (m_Panel = GetComponentInParent<IGuiPanel>()) : m_Panel; }
        }

        /// <summary>
        /// Layout position helper.
        /// </summary>
        public LayoutOffset LayoutOffset {
            get { return this.CacheComponent(ref m_LayoutOffset); }
        }

        /// <summary>
        /// Layout sizing helper.
        /// </summary>
        public LayoutSizeGroup LayoutSizeGroup {
            get { return this.CacheComponent(ref m_LayoutGroup); }
        }

        /// <summary>
        /// Canvas group.
        /// </summary>
        public CanvasGroup CanvasGroup {
            get { return this.CacheComponent(ref m_CanvasGroup); }
        }

        /// <summary>
        /// Pointer events and tooltips.
        /// </summary>
        public CursorHint CursorHint {
            get { return m_Cursor == null ? (m_Cursor = GetComponentInChildren<CursorHint>(true)) : m_Cursor; }
        }

        /// <summary>
        /// Primary renderer.
        /// </summary>
        public CanvasRenderer CanvasRenderer {
            get { return m_PrimaryGraphic ? m_PrimaryGraphic.canvasRenderer : null; }
        }

        /// <summary>
        /// Primary canvas graphic.
        /// </summary>
        public Graphic CanvasGraphic {
            get { return m_PrimaryGraphic; }
        }

        /// <summary>
        /// Primary canvas graphic, as an image.
        /// </summary>
        public Image ImageGraphic {
            get { return m_PrimaryGraphic as Image; }
        }

        /// <summary>
        /// Primary canvas graphic, as a text renderer.
        /// </summary>
        public TMP_Text TextGraphic {
            get { return m_PrimaryGraphic as TMP_Text; }
        }

        /// <summary>
        /// Color palette control.
        /// </summary>
        public IColorPaletteTint ColorTinter {
            get { return this.CacheComponent(ref m_Tint); }
        }

        #endregion // References

        #region State

        /// <summary>
        /// Returns the current state of the widget.
        /// </summary>
        public GuiWidgetStateFlags State {
            get { return m_StateFlags; }
        }

        /// <summary>
        /// Returns if the state of the widget contains all the given flags.
        /// </summary>
        public bool HasState(GuiWidgetStateFlags state) {
            return (m_StateFlags & state) == state;
        }

        /// <summary>
        /// Returns if the state of the widget contains any of the given flags.
        /// </summary>
        public bool HasAnyState(GuiWidgetStateFlags state) {
            return (m_StateFlags & state) != 0;
        }

        /// <summary>
        /// Returns if the widget is interactable.
        /// </summary>
        public bool Interactable {
            get { return (m_StateFlags & GuiWidgetStateFlags.PauseInteractions) == 0; }
            set {
                TryUpdateState(this, Bits.Set(m_StateFlags, GuiWidgetStateFlags.PauseInteractions, !value));
            }
        }

        static protected bool TryUpdateState(GuiWidget widget, GuiWidgetStateFlags state, GuiWidgetUpdateFlags flags = 0) {
            if (widget.m_StateFlags != state || (flags & GuiWidgetUpdateFlags.Force) != 0) {
                GuiWidgetStateFlags change = widget.m_StateFlags ^ state;
                widget.m_StateFlags = state;

                if (!widget.isActiveAndEnabled) {
                    flags |= GuiWidgetUpdateFlags.NoAnimation;
                }

                if (widget.CacheComponent(ref widget.m_CanvasGroup)) {
                    widget.m_CanvasGroup.blocksRaycasts = (state & GuiWidgetStateFlags.PauseInteractions) != 0;
                }
                widget.m_BaseStyle?.UpdateState(state, change, widget, flags);
                widget.UpdateState(state, change, flags);
                return true;
            }

            return false;
        }

        protected virtual void UpdateState(GuiWidgetStateFlags state, GuiWidgetStateFlags change, GuiWidgetUpdateFlags flags = 0) {
        }

        #endregion // State

        #region Events

        protected virtual void Awake() {
            if (m_Cursor && m_Cursor.Owner == null) {
                m_Cursor.Owner = this; 
            }
        }

        #endregion // Events

        #region Style

        /// <summary>
        /// The assigned widget style.
        /// </summary>
        public IGuiWidgetStyle Styler {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_BaseStyle; }
        }

        protected void AssignBaseStyle(IGuiWidgetStyle style) {
            Assert.NotNullOrDestroyed(style);
            m_BaseStyle = style;
            style.Bind(this);
            style.UpdateState(m_StateFlags, m_StateFlags, this, GuiWidgetUpdateFlags.Force | GuiWidgetUpdateFlags.NoAnimation);
        }

        #endregion // Style

        #region Interactable

        /// <summary>
        /// Evaluates the current interactable state of a widget.
        /// </summary>
        static public GuiWidgetInteractableState EvaluateInteractableState(GuiWidget widget) {
            if ((widget.m_StateFlags & GuiWidgetStateFlags.PauseInteractions) != 0) {
                return GuiWidgetInteractableState.Disabled;
            }

            CursorHint cursor = widget.CursorHint;
            if (cursor != null) {
                if (cursor.IsPointerEntered()) {
                    if (cursor.IsPointerDown()) {
                        return GuiWidgetInteractableState.Down;
                    }
                    return GuiWidgetInteractableState.Hover;
                }
            }

            return GuiWidgetInteractableState.Normal;
        }

        /// <summary>
        /// Attempts to update the interactable state of a widget.
        /// </summary>
        static protected bool TryUpdateInteractableState(GuiWidget widget, ref GuiWidgetInteractableState currentState, IGuiWidgetInteractiveStyle style, GuiWidgetUpdateFlags flags = 0) {
            GuiWidgetInteractableState targetState = EvaluateInteractableState(widget);
            if (currentState != targetState || (flags & GuiWidgetUpdateFlags.Force) != 0) {
                currentState = targetState;
                if (!widget.isActiveAndEnabled) {
                    flags |= GuiWidgetUpdateFlags.NoAnimation;
                }
                if (style != null) {
                    style.UpdateInteractionState(targetState, widget, flags);
                }
                return true;
            }

            return false;
        }

        #endregion // Interactable
    }

    /// <summary>
    /// Widget data range.
    /// </summary>
    public struct GuiDataWidgetRange<TValue> {
        public readonly TValue Min;
        public readonly TValue Max;

        public GuiDataWidgetRange(in TValue min, in TValue max) {
            Min = min;
            Max = max;
        }
    }

    /// <summary>
    /// Indicates that a widget contains a value of some type.
    /// </summary>
    public interface IGuiDataWidget {
        Variant GetVariantValue();
        void SetVariantValue(Variant variant, GuiWidgetUpdateFlags flags = 0);
        void ResetValue(GuiWidgetUpdateFlags flags = 0);
    }

    /// <summary>
    /// Indicates that a widget contains a value of a specific type.
    /// </summary>
    public interface IGuiDataWidget<TValue> : IGuiDataWidget {
        TValue Value { get; }
        void SetValue(TValue value, GuiWidgetUpdateFlags flags = 0);
    }
}