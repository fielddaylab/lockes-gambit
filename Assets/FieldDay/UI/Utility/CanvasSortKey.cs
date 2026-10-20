using BeauPools;
using BeauUtil;
using FieldDay.Rendering;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FieldDay.UI {
    /// <summary>
    /// Canvas render sorting key.
    /// </summary>
    public readonly struct CanvasSortKey {
        private const int CameraOrderBits = 8;
        private const int CameraOrderMask = (1 << CameraOrderBits) - 1;
        private const int CameraOrderOffset = RenderTypeOffset + RenderTypeBits;

        private const int RenderTypeBits = 2;
        private const int RenderTypeMask = (1 << RenderTypeBits) - 1;
        private const int RenderTypeOffset = SortingLayerOffset + SortingLayerBits;

        private const int SortingLayerBits = 8;
        private const int SortingLayerMask = (1 << SortingLayerBits) - 1;
        private const int SortingLayerOffset = SortingOrderOffset + SortingOrderBits;

        private const int SortingOrderBits = 16;
        private const int SortingOrderMask = (1 << SortingOrderBits) - 1;
        private const int SortingOrderOffset = PlaneDistanceOffset + PlaneDistanceBits;

        private const int PlaneDistanceBits = 12;
        private const int PlaneDistanceMask = (1 << PlaneDistanceBits) - 1;
        private const int PlaneDistanceOffset = 0;

        public readonly ulong RawValue;

        private CanvasSortKey(ulong value) {
            RawValue = value;
        }

        /// <summary>
        /// Creates a CanvasSortKey for the given Canvas.
        /// </summary>
        static public CanvasSortKey Create(Canvas canvas) {
            Canvas modeCanvas = canvas.rootCanvas;
            Canvas sortCanvas = canvas.overrideSorting ? canvas : modeCanvas;

            uint cameraOrder, renderType, planeDistance, sortingLayer, sortingOrder;
            sortingOrder = (uint) Math.Clamp(sortCanvas.sortingOrder + SortingOrderMask / 2, 0, SortingOrderMask);
            sortingLayer = (uint)Math.Clamp(sortCanvas.cachedSortingLayerValue + SortingLayerMask / 2, 0, SortingLayerMask);

            RenderMode renderMode = modeCanvas.renderMode;
            renderType = (uint) (3 - renderMode);

            modeCanvas.TryGetCamera(out Camera renderCam);
            cameraOrder = GetCameraOrder(renderCam);

            if (renderMode != RenderMode.ScreenSpaceCamera || !renderCam) {
                planeDistance = 0;
            } else {
                planeDistance = (uint)((1f - Math.Clamp(modeCanvas.planeDistance / renderCam.farClipPlane, 0, 1)) * PlaneDistanceMask);
            }

            return new CanvasSortKey(BitwiseKey(cameraOrder, renderType, planeDistance, sortingLayer, sortingOrder));
        }

        static public CanvasSortKey CreateForWorld(int sortingLayerId, int sortingOrder) {
            uint sortingLayer, sortingOrderUnsigned;
            sortingOrderUnsigned = (uint)Math.Clamp(sortingOrder + SortingOrderMask / 2, 0, SortingOrderMask);
            sortingLayer = (uint)Math.Clamp(SortingLayer.GetLayerValueFromID(sortingLayerId) + SortingLayerMask / 2, 0, SortingLayerMask);
            return new CanvasSortKey(BitwiseKey(0, 0, 0, sortingLayer, sortingOrderUnsigned));
        }

        static public CanvasSortKey CreateForWorld(Camera camera, int sortingLayerId, int sortingOrder) {
            uint sortingLayer, sortingOrderUnsigned;
            sortingOrderUnsigned = (uint)Math.Clamp(sortingOrder + SortingOrderMask / 2, 0, SortingOrderMask);
            sortingLayer = (uint)Math.Clamp(SortingLayer.GetLayerValueFromID(sortingLayerId) + SortingLayerMask / 2, 0, SortingLayerMask);
            return new CanvasSortKey(BitwiseKey(GetCameraOrder(camera), 0, 0, sortingLayer, sortingOrderUnsigned));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static private ulong BitwiseKey(uint cameraOrder, uint renderType, uint planeDistance, uint sortingLayer, uint sortingOrder) {
            return (ulong)(cameraOrder & CameraOrderMask) << CameraOrderOffset
                | (ulong)(renderType & RenderTypeMask) << RenderTypeOffset
                | (ulong)(planeDistance & PlaneDistanceMask) << PlaneDistanceOffset
                | (ulong)(sortingLayer & SortingLayerMask) << SortingLayerOffset
                | (ulong)(sortingOrder & SortingOrderMask) << SortingOrderOffset;
        }

        static private uint GetCameraOrder(Camera camera) {
            if (!camera) {
                return CameraOrderMask;
            }

            if (CameraUtility.IsOverlayCamera(camera)) {
                // TODO: Implement
                return (uint)Math.Clamp((int)camera.depth + CameraOrderMask / 2, 1, CameraOrderMask - 1);
            }

            return 0;
        }

        public override string ToString() {
            uint cameraOrder, renderType, planeDistance, sortingLayer, sortingOrder;

            cameraOrder = (uint) (RawValue >> CameraOrderOffset) & CameraOrderMask;
            renderType = (uint)(RawValue >> RenderTypeOffset) & RenderTypeMask;
            planeDistance = (uint)(RawValue >> PlaneDistanceOffset) & PlaneDistanceMask;
            sortingLayer = (uint)(RawValue >> SortingLayerOffset) & SortingLayerMask;
            sortingOrder = (uint)(RawValue >> SortingOrderOffset) & SortingOrderMask;

            using (PooledStringBuilder psb = PooledStringBuilder.Create()) {
                psb.Builder.AppendNoAlloc(cameraOrder, 3)
                    .Append(' ').AppendNoAlloc(renderType, 1)
                    .Append(' ').AppendNoAlloc(planeDistance, 4)
                    .Append(' ').AppendNoAlloc(sortingLayer, 3)
                    .Append(' ').AppendNoAlloc(sortingOrder, 5);
                return psb.Builder.ToString();
            }
        }
    }
}