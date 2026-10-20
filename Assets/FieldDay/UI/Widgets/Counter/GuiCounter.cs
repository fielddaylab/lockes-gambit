using BeauUtil;
using BeauUtil.Variants;
using FieldDay.Scenes;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FieldDay.UI.Widgets {
    public sealed class GuiCounter : GuiWidget, IGuiDataWidget<int>, IGuiDataPreviewWidget<int> {
        public abstract class Style : GuiWidgetDataStyle<int>, IGuiWidgetRangedDataStyle<int>, IGuiWidgetPreviewDataStyle<int> {
            public GuiCounter Widget { get; private set; }
            public override void Bind(GuiWidget source) {
                Widget = (GuiCounter)source;
            }
            public virtual void SetRange(in GuiDataWidgetRange<int> range, GuiWidget source, GuiWidgetUpdateFlags flags) { }
            public virtual void PopulatePreview(in GuiDataWidgetPreview<int> preview, GuiWidget source, GuiWidgetUpdateFlags flags) { }
            public override void UpdateState(GuiWidgetStateFlags state, GuiWidgetStateFlags changed, GuiWidget source, GuiWidgetUpdateFlags flags) { }
        }

        [SerializeField] private int m_StartingValue;
        [SerializeField] private int m_MaxValue;
        [SerializeField, Required] private Style m_Style;

        [NonSerialized] private int m_CurrentValue = -1;
        [NonSerialized] private int m_PreviewValue = -1;

        protected override void Awake() {
            base.Awake();

            if (!GameLoop.IsBooted()) {
                GameLoop.QueueOnBoot(Init);
            } else {
                Init();
            }
        }

        private void Init() {
            AssignBaseStyle(m_Style);
            m_Style.SetRange(new GuiDataWidgetRange<int>(0, m_MaxValue), this, GuiWidgetUpdateFlags.Initialization);
            if (m_CurrentValue < 0) {
                SetValue(m_StartingValue, GuiWidgetUpdateFlags.Initialization);
            }
        }

        public int MaxValue {
            get { return m_MaxValue; }
        }

        public int Value {
            get { return m_CurrentValue; }
        }

        public void SetValue(int value, GuiWidgetUpdateFlags flags = 0) {
            if ((flags & GuiWidgetUpdateFlags.Force) == 0 && (value == m_CurrentValue && m_PreviewValue < 0)) {
                return;
            }

            int lastValue = m_CurrentValue;
            value = Math.Clamp(value, 0, m_MaxValue);
            m_CurrentValue = value;
            m_PreviewValue = -1;

            if (value < lastValue) {
                flags |= GuiWidgetUpdateFlags.IsDecrease;
                flags &= ~GuiWidgetUpdateFlags.IsIncrease;
            } else if (value > lastValue) {
                flags |= GuiWidgetUpdateFlags.IsIncrease;
                flags &= ~GuiWidgetUpdateFlags.IsDecrease;
            } else {
                flags &= ~(GuiWidgetUpdateFlags.IsIncrease | GuiWidgetUpdateFlags.IsDecrease);
            }

            if (m_PreviewValue >= 0) {
                m_PreviewValue = -1;
                flags |= GuiWidgetUpdateFlags.PreviewEnd;
            }

            m_Style.Populate(value, this, flags);

            GuiWidgetStateFlags state = State;
            if (value > 0) {
                state = (state & ~GuiWidgetStateFlags.IsEmpty) | GuiWidgetStateFlags.CanDecrease;
            } else {
                state = (state | GuiWidgetStateFlags.IsEmpty) & ~GuiWidgetStateFlags.CanDecrease;
            }

            if (value < m_MaxValue) {
                state = (state & ~GuiWidgetStateFlags.IsFull) | GuiWidgetStateFlags.CanIncrease;
            } else {
                state = (state | GuiWidgetStateFlags.IsFull) & ~GuiWidgetStateFlags.CanIncrease;
            }

            TryUpdateState(this, state, flags);
        }

        public void SetPreview(int value, GuiWidgetUpdateFlags flags = 0) {
            if ((flags & GuiWidgetUpdateFlags.Force) == 0 && (value == m_CurrentValue || value == m_PreviewValue)) {
                return;
            }

            int lastValue = m_CurrentValue;
            value = Math.Clamp(value, 0, m_MaxValue);
            if (m_PreviewValue < 0) {
                flags |= GuiWidgetUpdateFlags.PreviewStart;
            } 
            m_PreviewValue = value;

            if (value < lastValue) {
                flags |= GuiWidgetUpdateFlags.IsDecrease;
                flags &= ~GuiWidgetUpdateFlags.IsIncrease;
            } else if (value > lastValue) {
                flags |= GuiWidgetUpdateFlags.IsIncrease;
                flags &= ~GuiWidgetUpdateFlags.IsDecrease;
            } else {
                flags &= ~(GuiWidgetUpdateFlags.IsIncrease | GuiWidgetUpdateFlags.IsDecrease);
            }

            m_Style.PopulatePreview(new GuiDataWidgetPreview<int>(lastValue, value), this, flags);
        }

        public void ClearPreview(GuiWidgetUpdateFlags flags = 0) {
            if ((flags & GuiWidgetUpdateFlags.Force) == 0 && m_PreviewValue < 0) {
                return;
            }

            m_PreviewValue = -1;
            if (m_PreviewValue >= 0) {
                flags |= GuiWidgetUpdateFlags.PreviewEnd;
            }

            m_Style.PopulatePreview(new GuiDataWidgetPreview<int>(m_CurrentValue, m_CurrentValue), this, flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetValue(GuiWidgetUpdateFlags flags = 0) {
            SetValue(m_StartingValue, flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Variant GetVariantValue() {
            return m_CurrentValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVariantValue(Variant value, GuiWidgetUpdateFlags flags = 0) {
            SetValue(value.AsInt(), flags);
        }
    }
}