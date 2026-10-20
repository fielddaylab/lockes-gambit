using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay.Collections;
using ScriptableBake;
using System;
using System.Runtime.CompilerServices;
using TMPro;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace FieldDay {
    [Il2CppEagerStaticClassConstruction]
    static public class Positioning {

        #region Queries

        /// <summary>
        /// Returns a temporary buffer containing all active immediate children of the given root.
        /// </summary>
        static public TempReferenceBuffer<Transform> QueryActiveChildren(this Transform root) {
            int count = root.childCount;
            if (count <= 0) {
                return default;
            }

            TempReferenceBuffer<Transform> temp = TempReferenceBuffer<Transform>.Create(count);
            QueryActiveChildren(root, temp);
            return temp;
        }

        /// <summary>
        /// Fills a temporary buffer containing all active immediate children of the given root.
        /// </summary>
        static public int QueryActiveChildren(this Transform root, TempReferenceBuffer<Transform> buffer) {
            int count = root.childCount;
            for (int i = 0; i < count; i++) {
                Transform t = root.GetChild(i);
                if (t.gameObject.activeSelf) {
                    buffer.Add(t);
                }
            }
            return count;
        }

        /// <summary>
        /// Returns a temporary buffer containing all active immediate children of the given root.
        /// </summary>
        static public TempReferenceBuffer<RectTransform> QueryActiveChildren(this RectTransform root) {
            int count = root.childCount;
            if (count <= 0) {
                return default;
            }

            TempReferenceBuffer<RectTransform> temp = TempReferenceBuffer<RectTransform>.Create(count);
            QueryActiveChildren(root, temp);
            return temp;
        }

        /// <summary>
        /// Returns a temporary buffer containing all active immediate children of the given root.
        /// Will ignore children with an IgnoreLayout LayoutElement.
        /// </summary>
        static public TempReferenceBuffer<RectTransform> QueryLayoutChildren(this RectTransform root) {
            int count = root.childCount;
            if (count <= 0) {
                return default;
            }

            TempReferenceBuffer<RectTransform> temp = TempReferenceBuffer<RectTransform>.Create(count);
            QueryLayoutChildren(root, temp);
            return temp;
        }

        /// <summary>
        /// Fills a temporary buffer containing all active immediate children of the given root.
        /// </summary>
        static public int QueryActiveChildren(this RectTransform root, TempReferenceBuffer<RectTransform> buffer) {
            int count = root.childCount;
            for(int i = 0; i < count; i++) {
                Transform t = root.GetChild(i);
                if (t.gameObject.activeSelf && t is RectTransform) {
                    buffer.Add(Unsafe.FastCast<RectTransform>(t));
                }
            }
            return count;
        }

        /// <summary>
        /// Fills a temporary buffer containing all active immediate children of the given root.
        /// Will ignore children with an IgnoreLayout LayoutElement.
        /// </summary>
        static public int QueryLayoutChildren(this RectTransform root, TempReferenceBuffer<RectTransform> buffer) {
            int count = root.childCount;
            for (int i = 0; i < count; i++) {
                Transform t = root.GetChild(i);
                if (t.gameObject.activeSelf) {
                    if (t.TryGetComponent(out LayoutElement elem) && elem.enabled && elem.ignoreLayout) {
                        continue;
                    }
                    buffer.Add(Unsafe.FastCast<RectTransform>(t));
                }
            }
            return count;
        }

        #endregion // Queries

        #region Anchors

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static private float GetAnchorX(TextAnchor anchor) {
            return ((int)anchor % 3) * 0.5f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static private float GetAnchorY(TextAnchor anchor) {
            return (2 - (int)anchor / 3) * 0.5f;
        }

        /// <summary>
        /// Sets the anchor for the given RectTransform on the x-axis.
        /// </summary>
        static public void SetAnchorX(RectTransform rect, float anchorX) {
            Vector2 min, max;
            min = rect.anchorMin;
            max = rect.anchorMax;
            min.x = max.x = anchorX;
            rect.anchorMin = min;
            rect.anchorMax = max;
        }

        /// <summary>
        /// Sets the anchor for the given RectTransform on the y-axis.
        /// </summary>
        static public void SetAnchorY(RectTransform rect, float anchorY) {
            Vector2 min, max;
            min = rect.anchorMin;
            max = rect.anchorMax;
            min.y = max.y = anchorY;
            rect.anchorMin = min;
            rect.anchorMax = max;
        }

        /// <summary>
        /// Sets the anchor for the given RectTransform.
        /// </summary>
        static public void SetAnchor(RectTransform rect, Vector2 anchorXY) {
            rect.anchorMin = anchorXY;
            rect.anchorMax = anchorXY;
        }

        /// <summary>
        /// Sets the anchor for the given RectTransform.
        /// </summary>
        static public void SetAnchor(RectTransform rect, TextAnchor anchor) {
            Vector2 anchorXY = new Vector2(GetAnchorX(anchor), GetAnchorY(anchor));
            rect.anchorMin = anchorXY;
            rect.anchorMax = anchorXY;
        }

        /// <summary>
        /// Sets the anchor and offset for the given RectTransform on the x-axis.
        /// </summary>
        static public void SetAnchorOffsetX(RectTransform rect, float anchorX, float offsetX) {
            Vector2 min, max, offset;
            min = rect.anchorMin;
            max = rect.anchorMax;
            offset = rect.anchoredPosition;
            min.x = max.x = anchorX;
            offset.x = offsetX;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.anchoredPosition = offset;
        }

        /// <summary>
        /// Sets the anchor and offset for the given RectTransform on the y-axis.
        /// </summary>
        static public void SetAnchorOffsetY(RectTransform rect, float anchorY, float offsetY) {
            Vector2 min, max, offset;
            min = rect.anchorMin;
            max = rect.anchorMax;
            offset = rect.anchoredPosition;
            min.x = max.y = anchorY;
            offset.y = offsetY;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.anchoredPosition = offset;
        }

        /// <summary>
        /// Sets the anchor and offset for the given RectTransform.
        /// </summary>
        static public void SetAnchorOffset(RectTransform rect, Vector2 anchorXY, Vector2 offset) {
            rect.anchorMin = anchorXY;
            rect.anchorMax = anchorXY;
            rect.anchoredPosition = offset;
        }

        /// <summary>
        /// Sets the anchor and offset for the given RectTransform.
        /// </summary>
        static public void SetAnchorOffset(RectTransform rect, TextAnchor anchor, Vector2 offset) {
            Vector2 anchorXY = new Vector2(GetAnchorX(anchor), GetAnchorY(anchor));
            rect.anchorMin = anchorXY;
            rect.anchorMax = anchorXY;
            rect.anchoredPosition = offset;
        }

        /// <summary>
        /// Sets the offset from the anchor on the x-axis for the given RectTransform.
        /// </summary>
        static public void SetOffsetX(RectTransform rect, float offsetX) {
            Vector2 anchorPos = rect.anchoredPosition;
            anchorPos.x = offsetX;
            rect.anchoredPosition = anchorPos;
        }

        /// <summary>
        /// Sets the offset from the anchor on the y-axis for the given RectTransform.
        /// </summary>
        static public void SetOffsetY(RectTransform rect, float offsetY) {
            Vector2 anchorPos = rect.anchoredPosition;
            anchorPos.y = offsetY;
            rect.anchoredPosition = anchorPos;
        }

        /// <summary>
        /// Sets the offset from the anchor for the given RectTransform.
        /// </summary>
        static public void SetOffset(RectTransform rect, float offsetX, float offsetY) {
            rect.anchoredPosition = new Vector2(offsetX, offsetY);
        }

        #endregion // Anchors

        #region Pivot

        /// <summary>
        /// Sets the pivot point for the given RectTransform.
        /// </summary>
        static public void SetPivot(RectTransform rect, TextAnchor pivot) {
            rect.pivot = new Vector2(GetAnchorX(pivot), GetAnchorY(pivot));
        }

        /// <summary>
        /// Sets the pivot point on the x-axis for the given RectTransform.
        /// </summary>
        static public void SetPivotX(RectTransform rect, float pivotX) {
            Vector2 pivot = rect.pivot;
            pivot.x = pivotX;
            rect.pivot = pivot;
        }

        /// <summary>
        /// Sets the pivot point on the y-axis for the given RectTransform.
        /// </summary>
        static public void SetPivotY(RectTransform rect, float pivotY) {
            Vector2 pivot = rect.pivot;
            pivot.y = pivotY;
            rect.pivot = pivot;
        }

        #endregion // Pivot

        #region Local Position

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public void SetLocalX(Transform transform, float x) {
            Vector3 pos = transform.localPosition;
            pos.x = x;
            transform.localPosition = pos;
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public void SetLocalY(Transform transform, float y) {
            Vector3 pos = transform.localPosition;
            pos.y = y;
            transform.localPosition = pos;
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public void SetLocalZ(Transform transform, float z) {
            Vector3 pos = transform.localPosition;
            pos.z = z;
            transform.localPosition = pos;
        }

        #endregion // Local Position

        #region Size Delta

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public void SetSizeDelta(RectTransform rect, Vector2 sizeDelta) {
            rect.sizeDelta = sizeDelta;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public void SetSizeDelta(RectTransform rect, float widthDelta, float heightDelta) {
            rect.sizeDelta = new Vector2(widthDelta, heightDelta);
        }

        static public void SetWidthDelta(RectTransform rect, float widthDelta) {
            Vector2 sizeDelta = rect.sizeDelta;
            sizeDelta.x = widthDelta;
            rect.sizeDelta = sizeDelta;
        }

        static public void SetHeightDelta(RectTransform rect, float heightDelta) {
            Vector2 sizeDelta = rect.sizeDelta;
            sizeDelta.y = heightDelta;
            rect.sizeDelta = sizeDelta;
        }

        #endregion // Size Delta

        #region Horizontal Layout

        /// <summary>
        /// Horizontally lays out given set of RectTransforms.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public LayoutResult HorizontalLayout(TempReferenceBuffer<RectTransform> buffer, in LayoutOptions options, float basePosition = 0) {
            return DoHorizontalLayoutRect(buffer, options, basePosition, default);
        }

        /// <summary>
        /// Horizontally lays out given set of RectTransforms.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public LayoutResult DeferredHorizontalLayout(TempReferenceBuffer<RectTransform> buffer, in LayoutOptions options, float basePosition, UnsafeSpan<float> outputValues) {
            return DoHorizontalLayoutRect(buffer, options, basePosition, outputValues);
        }

        static private unsafe LayoutResult DoHorizontalLayoutRect(TempReferenceBuffer<RectTransform> buffer, in LayoutOptions options, float basePosition, UnsafeSpan<float> deferredPositions) {
            int len = buffer.Count;

            if (len == 0) {
                return default;
            }

            float* offsets = stackalloc float[len];
            float* pivots = stackalloc float[len];
            float* sizes = stackalloc float[len];
            float* paddingBefore = stackalloc float[len];
            float* paddingAfter = stackalloc float[len];

            float totalSize = 0;

            RectTransform rect;
            LayoutStyle style;
            switch (options.Source) {
                case LayoutSource.PreferredSize: {
                    for (int i = 0; i < len; i++) {
                        rect = buffer[i];
                        sizes[i] = GetPreferredWidth(rect);
                        pivots[i] = rect.pivot.x;
                        GetStyle(rect, out style);
                        paddingBefore[i] = style.MarginLower.x;
                        paddingAfter[i] = style.MarginUpper.x;
                    }
                    totalSize = ProcessPositionsDynamicSize(len, sizes, pivots, paddingBefore, paddingAfter, options.Spacing, offsets);
                    break;
                }
                case LayoutSource.Size: {
                    for (int i = 0; i < len; i++) {
                        rect = buffer[i];
                        sizes[i] = rect.rect.width;
                        pivots[i] = rect.pivot.x;
                        GetStyle(rect, out style);
                        paddingBefore[i] = style.MarginLower.x;
                        paddingAfter[i] = style.MarginUpper.x;
                    }
                    totalSize = ProcessPositionsDynamicSize(len, sizes, pivots, paddingBefore, paddingAfter, options.Spacing, offsets);
                    break;
                }
                case LayoutSource.FixedSize: {
                    for (int i = 0; i < len; i++) {
                        rect = buffer[i];
                        pivots[i] = rect.pivot.y;
                    }
                    totalSize = ProcessPositionsFixedSize(len, options.FixedSize, pivots, options.Spacing, offsets);
                    break;
                }
            }

            basePosition = ComputeBasePosition(basePosition, totalSize, options.NormalizedAlignment);

            if (deferredPositions) {
                Assert.True(deferredPositions.Length >= len, "Not enough space in deferred layout storage");
                for (int i = 0; i < len; i++) {
                    deferredPositions[i] = basePosition + offsets[i];
                }
            } else {
                for (int i = 0; i < len; i++) {
                    rect = buffer[i];
#if UNITY_EDITOR
                    Baking.PrepareUndo(rect, "Horizontal alignment");
#endif // UNITY_EDITOR
                    bool hasLayoutOffset = rect.TryGetComponent(out LayoutOffset offset);
                    if (hasLayoutOffset) {
                        offset.BeginLayoutChanges();
                    }
                    Vector2 anchoredPos = rect.anchoredPosition;
                    anchoredPos.x = basePosition + offsets[i];
                    rect.anchoredPosition = anchoredPos;
                    if (hasLayoutOffset) {
                        offset.EndLayoutChanges();
                    }
                }
            }

            return new LayoutResult() {
                Size = totalSize
            };
        }

        /// <summary>
        /// Horizontally aligns the given set of RectTransforms.
        /// </summary>
        static public void HorizontalAlign(TempReferenceBuffer<RectTransform> buffer, float basePosition) {
            int len = buffer.Count;

            if (len == 0) {
                return;
            }

            RectTransform rect;
            Vector3 anchorPos;
            for(int i = 0; i < len; i++) {
                rect = buffer[i];
                bool hasLayoutOffset = rect.TryGetComponent(out LayoutOffset offset);
                if (hasLayoutOffset) {
                    offset.BeginLayoutChanges();
                }
                anchorPos = rect.anchoredPosition3D;
                anchorPos.x = basePosition;
                rect.anchoredPosition3D = anchorPos;
                if (hasLayoutOffset) {
                    offset.EndLayoutChanges();
                }
            }
        }

        /// <summary>
        /// Returns the maximum width of the given set of RectTransforms;
        /// </summary>
        static public float CalculateMaxWidth(TempReferenceBuffer<RectTransform> buffer, in LayoutOptions options) {
            int len = buffer.Count;

            if (len == 0) {
                return 0;
            }

            if (options.Source == LayoutSource.FixedSize) {
                return options.FixedSize;
            }

            float size = 0;
            RectTransform rect;
            switch (options.Source) {
                case LayoutSource.PreferredSize: {
                    for (int i = 0; i < len; i++) {
                        rect = buffer[i];
                        size = Math.Max(size, GetPreferredWidth(rect));
                    }
                    break;
                }
                case LayoutSource.Size: {
                    for (int i = 0; i < len; i++) {
                        rect = buffer[i];
                        size = Math.Max(size, rect.rect.width);
                    }
                    break;
                }
            }

            return size;
        }

        #endregion // Horizontal Layout

        #region Vertical Layout

        /// <summary>
        /// Vertically lays out given set of RectTransforms.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public LayoutResult VerticalLayout(TempReferenceBuffer<RectTransform> buffer, in LayoutOptions options, float basePosition = 0) {
            return DoVerticalLayoutRect(buffer, options, basePosition, default);
        }
        /// <summary>
        /// Vertically lays out given set of RectTransforms.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public LayoutResult DeferredVerticalLayout(TempReferenceBuffer<RectTransform> buffer, in LayoutOptions options, float basePosition, UnsafeSpan<float> outputValues) {
            return DoVerticalLayoutRect(buffer, options, basePosition, outputValues);
        }

        static private unsafe LayoutResult DoVerticalLayoutRect(TempReferenceBuffer<RectTransform> buffer, in LayoutOptions options, float basePosition, UnsafeSpan<float> deferredPositions) {
            int len = buffer.Count;

            if (len == 0) {
                return default;
            }

            float* offsets = stackalloc float[len];
            float* pivots = stackalloc float[len];
            float* sizes = stackalloc float[len];
            float* paddingBefore = stackalloc float[len];
            float* paddingAfter = stackalloc float[len];
            float totalSize = 0;

            float direction = -1;
            bool flipPivot = false;
            if ((options.Flags & LayoutFlags.VerticalLayoutUp) != 0) {
                direction = 1;
                flipPivot = true;
            }

            RectTransform rect;
            LayoutStyle style;
            switch (options.Source) {
                case LayoutSource.PreferredSize: {
                    for (int i = 0; i < len; i++) {
                        rect = buffer[i];
                        sizes[i] = GetPreferredHeight(rect);
                        pivots[i] = ConditionalFlipPivot(rect.pivot.y, flipPivot);
                        GetStyle(rect, out style);
                        paddingBefore[i] = flipPivot ? style.MarginLower.y : style.MarginUpper.y;
                        paddingAfter[i] = flipPivot ? style.MarginUpper.y : style.MarginLower.y;
                    }
                    totalSize = ProcessPositionsDynamicSize(len, sizes, pivots, paddingBefore, paddingAfter, options.Spacing, offsets);
                    break;
                }
                case LayoutSource.Size: {
                    for (int i = 0; i < len; i++) {
                        rect = buffer[i];
                        sizes[i] = rect.rect.height;
                        pivots[i] = ConditionalFlipPivot(rect.pivot.y, flipPivot);
                        GetStyle(rect, out style);
                        paddingBefore[i] = flipPivot ? style.MarginLower.y : style.MarginUpper.y;
                        paddingAfter[i] = flipPivot ? style.MarginUpper.y : style.MarginLower.y;
                    }
                    totalSize = ProcessPositionsDynamicSize(len, sizes, pivots, paddingBefore, paddingAfter, options.Spacing, offsets);
                    break;
                }
                case LayoutSource.FixedSize: {
                    for (int i = 0; i < len; i++) {
                        rect = buffer[i];
                        pivots[i] = ConditionalFlipPivot(rect.pivot.y, flipPivot);
                    }
                    totalSize = ProcessPositionsFixedSize(len, options.FixedSize, pivots, options.Spacing, offsets);
                    break;
                }
            }

            basePosition = ComputeBasePosition(basePosition, direction * totalSize, ConditionalFlipPivot(options.NormalizedAlignment, flipPivot));

            if (deferredPositions) {
                Assert.True(deferredPositions.Length >= len, "Not enough space in deferred layout storage");
                for (int i = 0; i < len; i++) {
                    deferredPositions[i] = basePosition + direction * offsets[i];
                }
            } else {
                for (int i = 0; i < len; i++) {
                    rect = buffer[i];
#if UNITY_EDITOR
                    Baking.PrepareUndo(rect, "Vertical alignment");
#endif // UNITY_EDITOR
                    bool hasLayoutOffset = rect.TryGetComponent(out LayoutOffset offset);
                    if (hasLayoutOffset) {
                        offset.BeginLayoutChanges();
                    }
                    Vector2 anchoredPos = rect.anchoredPosition;
                    anchoredPos.y = basePosition + direction * offsets[i];
                    rect.anchoredPosition = anchoredPos;
                    if (hasLayoutOffset) {
                        offset.EndLayoutChanges();
                    }
                }
            }

            return new LayoutResult() {
                Size = totalSize
            };
        }

        /// <summary>
        /// Vertically aligns the given set of RectTransforms.
        /// </summary>
        static public void VerticalAlign(TempReferenceBuffer<RectTransform> buffer, float basePosition) {
            int len = buffer.Count;

            if (len == 0) {
                return;
            }

            RectTransform rect;
            Vector3 anchorPos;
            for (int i = 0; i < len; i++) {
                rect = buffer[i];
                bool hasLayoutOffset = rect.TryGetComponent(out LayoutOffset offset);
                if (hasLayoutOffset) {
                    offset.BeginLayoutChanges();
                }
                anchorPos = rect.anchoredPosition3D;
                anchorPos.y = basePosition;
                rect.anchoredPosition3D = anchorPos;
                if (hasLayoutOffset) {
                    offset.EndLayoutChanges();
                }
            }
        }

        /// <summary>
        /// Returns the maximum height of the given set of RectTransforms;
        /// </summary>
        static public float CalculateMaxHeight(TempReferenceBuffer<RectTransform> buffer, in LayoutOptions options) {
            int len = buffer.Count;

            if (len == 0) {
                return 0;
            }

            if (options.Source == LayoutSource.FixedSize) {
                return options.FixedSize;
            }

            float size = 0;
            RectTransform rect;
            switch (options.Source) {
                case LayoutSource.PreferredSize: {
                    for (int i = 0; i < len; i++) {
                        rect = buffer[i];
                        size = Math.Max(size, GetPreferredHeight(rect));
                    }
                    break;
                }
                case LayoutSource.Size: {
                    for (int i = 0; i < len; i++) {
                        rect = buffer[i];
                        size = Math.Max(size, rect.rect.height);
                    }
                    break;
                }
            }

            return size;
        }

        #endregion // Vertical Layout

        #region Axis Layout

        /// <summary>
        /// Lays out given set of Transforms along the given axis.
        /// </summary>
        static public LayoutResult AxisLayout(TempReferenceBuffer<Transform> buffer, in LayoutOptions options, float basePosition, Axis axis) {
            return DoAxisLayout(buffer, options, basePosition, axis, default);
        }

        static private unsafe LayoutResult DoAxisLayout(TempReferenceBuffer<Transform> buffer, in LayoutOptions options, float basePosition, Axis axis, UnsafeSpan<float> deferredPositions) {
            int len = buffer.Count;

            if (len == 0) {
                return default;
            }

            Assert.True(axis == Axis.X || axis == Axis.Y || axis == Axis.Z, "Invalid axis");
            int axisIndex = Bits.IndexOf(axis);

            float* offsets = stackalloc float[len];
            float* pivots = stackalloc float[len];
            float* sizes = stackalloc float[len];
            float* paddingBefore = stackalloc float[len];
            float* paddingAfter = stackalloc float[len];
            float totalSize = 0;

            Transform transform;
            LayoutSizeInfo sizeInfo;
            switch (options.Source) {
                case LayoutSource.PreferredSize:
                case LayoutSource.Size: {
                    for (int i = 0; i < len; i++) {
                        transform = buffer[i];
                        if (transform.TryGetComponent(out sizeInfo)) {
                            pivots[i] = sizeInfo.Pivot[axisIndex];
                            sizes[i] = sizeInfo.Size[axisIndex];
                        } else {
                            pivots[i] = 0.5f;
                            sizes[i] = transform.localScale[axisIndex];
                        }
                    }
                    totalSize = ProcessPositionsDynamicSize(len, sizes, pivots, paddingBefore, paddingAfter, options.Spacing, offsets);
                    break;
                }
                case LayoutSource.FixedSize: {
                    for (int i = 0; i < len; i++) {
                        transform = buffer[i];
                        if (transform.TryGetComponent(out sizeInfo)) {
                            pivots[i] = sizeInfo.Pivot[axisIndex];
                        } else {
                            pivots[i] = 0.5f;
                        }
                    }
                    totalSize = ProcessPositionsFixedSize(len, options.FixedSize, pivots, options.Spacing, offsets);
                    break;
                }
            }

            basePosition = ComputeBasePosition(basePosition, totalSize, options.NormalizedAlignment);

            if (deferredPositions) {
                for (int i = 0; i < len; i++) {
                    deferredPositions[i] = basePosition + offsets[i];
                }
            } else {
                for (int i = 0; i < len; i++) {
                    transform = buffer[i];
#if UNITY_EDITOR
                    Baking.PrepareUndo(transform, "Axis alignment");
#endif // UNITY_EDITOR
                    Vector3 localPos = transform.localPosition;
                    localPos[axisIndex] = basePosition + offsets[i];
                    transform.localPosition = localPos;
                }
            }

            return new LayoutResult() {
                Size = totalSize
            };
        }

        #endregion // Axis Layout

        #region Preferred

        /// <summary>
        /// Resizes the given RectTransform to the given size.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public void SetSizeWithCurrentAnchors(RectTransform rect, Vector2 size) {
            SetSizeWithCurrentAnchors(rect, size.x, size.y);
        }

        /// <summary>
        /// Resizes the given RectTransform to the given size.
        /// </summary>
        static public void SetSizeWithCurrentAnchors(RectTransform rect, float width, float height) {
            Vector2 parentSize = default;
            RectTransform parent = rect.parent as RectTransform;
            if (parent) {
                parentSize = parent.sizeDelta;
            }

            Vector2 anchorMin = rect.anchorMin,
                anchorMax = rect.anchorMax;

            Vector2 newSizeDelta;
            newSizeDelta.x = width - parentSize.x * (anchorMax.x - anchorMin.x);
            newSizeDelta.y = height - parentSize.y * (anchorMax.y - anchorMin.y);
            rect.sizeDelta = newSizeDelta;
        }

        /// <summary>
        /// Resizes the RectTransform to the given ILayoutElement's preferred width and height.
        /// </summary>
        static public void ResizeToPreferred(RectTransform rect, ILayoutElement layoutElement) {
            SetSizeWithCurrentAnchors(rect, layoutElement.preferredWidth, layoutElement.preferredHeight);
        }

        /// <summary>
        /// Resizes the RectTransform to the given ILayoutElement's preferred width.
        /// </summary>
        static public void ResizeToPreferredWidth(RectTransform rect, ILayoutElement layoutElement) {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, layoutElement.preferredWidth);
        }

        /// <summary>
        /// Resizes the RectTransform to the given ILayoutElement's preferred height.
        /// </summary>
        static public void ResizeToPreferredHeight(RectTransform rect, ILayoutElement layoutElement) {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, layoutElement.preferredHeight);
        }

        /// <summary>
        /// Resizes the RectTransform to the given ILayoutElement's preferred width and height.
        /// </summary>
        static public void ResizeToPreferred<TGraphic>(TGraphic graphic) where TGraphic : Graphic, ILayoutElement {
            SetSizeWithCurrentAnchors(graphic.rectTransform, graphic.preferredWidth, graphic.preferredHeight);
        }

        /// <summary>
        /// Resizes the RectTransform to the given ILayoutElement's preferred width.
        /// </summary>
        static public void ResizeToPreferredWidth<TGraphic>(TGraphic graphic) where TGraphic : Graphic, ILayoutElement {
            graphic.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, graphic.preferredWidth);
        }

        /// <summary>
        /// Resizes the RectTransform to the given ILayoutElement's preferred height.
        /// </summary>
        static public void ResizeToPreferredHeight<TGraphic>(TGraphic graphic) where TGraphic : Graphic, ILayoutElement {
            graphic.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, graphic.preferredHeight);
        }

        /// <summary>
        /// Resizes the RectTransform to the given ILayoutElement's preferred width and height.
        /// </summary>
        static public void ResizeToPreferred(TMP_Text graphic) {
            SetSizeWithCurrentAnchors(graphic.rectTransform, graphic.preferredWidth, graphic.preferredHeight);
        }

        /// <summary>
        /// Resizes the RectTransform to the given ILayoutElement's preferred width.
        /// </summary>
        static public void ResizeToPreferredWidth(TMP_Text graphic) {
            graphic.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, graphic.preferredWidth);
        }

        /// <summary>
        /// Resizes the RectTransform to the given ILayoutElement's preferred height.
        /// </summary>
        static public void ResizeToPreferredHeight(TMP_Text graphic) {
            graphic.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, graphic.preferredHeight);
        }

        #endregion // Preferred

        #region Property Retrieval

        [Il2CppSetOption(Option.NullChecks, false)]
        static private float GetPreferredWidth(RectTransform rect) {
            if (rect.TryGetComponent(out LayoutSizeInfo sizeInfo)) {
                return sizeInfo.Size.x;
            } else {
                float size = LayoutUtility.GetPreferredWidth(rect);
                if (size <= 0) {
                    size = rect.rect.width;
                }
                return size;
            }
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        static private float GetPreferredHeight(RectTransform rect) {
            if (rect.TryGetComponent(out LayoutSizeInfo sizeInfo)) {
                return sizeInfo.Size.y;
            } else {
                float size = LayoutUtility.GetPreferredHeight(rect);
                if (size <= 0) {
                    size = rect.rect.height;
                }
                return size;
            }
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        static private void GetStyle(RectTransform rect, out LayoutStyle style) {
            if (rect.TryGetComponent(out LayoutStyleInfo styleInfo)) {
                style = styleInfo.Style;
            } else {
                style = default;
            }
        }

        #endregion // Property Retrieval

        #region Layout Math

        [Il2CppSetOption(Option.NullChecks, false)]
        static private unsafe float ProcessPositionsDynamicSize(int entryCount, float* sizes, float* pivots, float* paddingBefore, float* paddingAfter, float spacing, float* results) {
            float total = 0;
            float size;
            for(int i = 0; i < entryCount; i++) {
                total += paddingBefore[i];
                size = sizes[i];
                results[i] = total + (1 - pivots[i]) * size;
                total += spacing + size + paddingAfter[i];
            }
            total -= spacing;
            return total;
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        static private unsafe float ProcessPositionsFixedSize(int entryCount, float size, float* pivots, float spacing, float* results) {
            for (int i = 0; i < entryCount; i++) {
                results[i] = i * (spacing + size) + (1 - pivots[i]) * size;
            }
            float total = (spacing + size) * entryCount - size;
            return total;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static private float ComputeBasePosition(float originalBasePosition, float totalSize, float normalizedAlignment) {
            return originalBasePosition - (totalSize * (1 - normalizedAlignment));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static private float ConditionalFlipPivot(float pivot, bool flip) {
            return flip ? (1 - pivot) : pivot;
        }

        #endregion // Layout Math
    }

    public enum LayoutSource : byte {
        FixedSize,
        PreferredSize,
        Size,
    }

    [Flags]
    public enum LayoutFlags : ushort {
        VerticalLayoutUp = 0x01
    }

    public struct LayoutResult {
        public float Size;
    }

    [Serializable]
    public struct LayoutStyle {
        public Vector3 MarginLower;
        public Vector3 MarginUpper;
        //public Vector3 PaddingLower;
        //public Vector3 PaddingUpper;
    }

    [Serializable]
    public struct LayoutOptions {
        public LayoutSource Source;
        public LayoutFlags Flags;
        public float NormalizedAlignment;
        public float Spacing;
        [ShowIfField("DisplayFixedSize")] public float FixedSize;

#if UNITY_EDITOR
        private bool DisplayFixedSize() {
            return Source == LayoutSource.FixedSize;
        }
#endif // UNITY_EDITOR

        static public LayoutOptions Fixed(float fixedSize, float spacing, float alignment = 0.5f) {
            return new LayoutOptions() {
                Source = LayoutSource.FixedSize,
                FixedSize = fixedSize,
                Spacing = spacing,
                NormalizedAlignment = alignment
            };
        }

        static public LayoutOptions Size(float spacing, float alignment = 0.5f) {
            return new LayoutOptions() {
                Source = LayoutSource.Size,
                Spacing = spacing,
                NormalizedAlignment = alignment
            };
        }

        static public LayoutOptions PreferredSize(float spacing, float alignment = 0.5f) {
            return new LayoutOptions() {
                Source = LayoutSource.PreferredSize,
                Spacing = spacing,
                NormalizedAlignment = alignment
            };
        }
    }
}