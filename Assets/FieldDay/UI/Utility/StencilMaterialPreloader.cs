using BeauUtil;
using BeauUtil.Debugger;
using FieldDay.Scenes;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace FieldDay.UI {
    /// <summary>
    /// Preloads mask and maskable material variants for the given UI materials.
    /// This will prevent Unity from repeatedly creating and destroying Materials
    /// if masking is enabled/disabled repeatedly.
    /// </summary>
    public sealed class StencilMaterialPreloader : MonoBehaviour {
        [SerializeField, Range(1, 8)] private int m_MaxMaskDepth = 1;
        [SerializeField] private Material[] m_MaskMaterials;
        [SerializeField] private Material[] m_MaskableMaterials;

        [NonSerialized] private Material[] m_GeneratedMaterials;

        private void Awake() {
            GenerateMasks();
        }

        private void OnDestroy() {
            DestroyMasks();
        }

        private void GenerateMasks() {
            if (m_GeneratedMaterials != null) {
                return;
            }

            m_GeneratedMaterials = new Material[GetGeneratedMaterialCount()];

            int writeHead = 0;
            using (Profiling.Time("preloading stencil variants", ProfileTimeUnits.Microseconds)) {
                for (int i = 0; i < m_MaxMaskDepth; i++) {
                    GenerateStencilMasks(i, ref writeHead);
                    GenerateStencilMaskables(i + 1, ref writeHead);
                }
            }

            Assert.True(writeHead == m_GeneratedMaterials.Length, "Estimation was incorrect");
            Log.Msg("[StencilMaterialPreloader] Generated {0} stencil materials!", writeHead);
        }

        private void GenerateStencilMasks(int depth, ref int writeHead) {
            int stencilBit = 1 << depth;

            if (stencilBit == 1) {
                GenerateInitialStencilMasks(ref writeHead);
                return;
            }

            int stencilBitFullMask = stencilBit | (stencilBit - 1);

            for (int i = 0; i < m_MaskMaterials.Length; i++) {
                //Log.Msg("[StencilMaterialPreloader] Generating Mask material variants for material '{0}', depth {1}", m_MaskMaterials[i].name, depth);
                // invisible
                m_GeneratedMaterials[writeHead++] = StencilMaterial.Add(m_MaskMaterials[i], stencilBitFullMask, StencilOp.Replace, CompareFunction.Equal, (ColorWriteMask)0, stencilBit - 1, stencilBitFullMask); // push
                m_GeneratedMaterials[writeHead++] = StencilMaterial.Add(m_MaskMaterials[i], stencilBit - 1, StencilOp.Replace, CompareFunction.Equal, (ColorWriteMask)0, stencilBit - 1, stencilBitFullMask); // pop

                // visible
                m_GeneratedMaterials[writeHead++] = StencilMaterial.Add(m_MaskMaterials[i], stencilBitFullMask, StencilOp.Replace, CompareFunction.Equal, ColorWriteMask.All, stencilBit - 1, stencilBitFullMask); // push
                m_GeneratedMaterials[writeHead++] = StencilMaterial.Add(m_MaskMaterials[i], stencilBit - 1, StencilOp.Replace, CompareFunction.Equal, (ColorWriteMask)0, stencilBit - 1, stencilBitFullMask); // pop
            }
        }

        private void GenerateInitialStencilMasks(ref int writeHead) {
            for (int i = 0; i < m_MaskMaterials.Length; i++) {
                //Log.Msg("[StencilMaterialPreloader] Generating Mask material variants for material '{0}', depth {1}", m_MaskMaterials[i].name, 0);
                // invisible
                m_GeneratedMaterials[writeHead++] = StencilMaterial.Add(m_MaskMaterials[i], 1, StencilOp.Replace, CompareFunction.Always, (ColorWriteMask)0); // push
                m_GeneratedMaterials[writeHead++] = StencilMaterial.Add(m_MaskMaterials[i], 1, StencilOp.Zero, CompareFunction.Always, (ColorWriteMask)0); // pop

                // visible
                m_GeneratedMaterials[writeHead++] = StencilMaterial.Add(m_MaskMaterials[i], 1, StencilOp.Replace, CompareFunction.Always, ColorWriteMask.All); // push
                m_GeneratedMaterials[writeHead++] = StencilMaterial.Add(m_MaskMaterials[i], 1, StencilOp.Zero, CompareFunction.Always, (ColorWriteMask)0); // pop
            }
        }

        private void GenerateStencilMaskables(int depth, ref int writeHead) {
            int stencilMask = (1 << depth) - 1;

            for(int i = 0; i < m_MaskableMaterials.Length; i++) {
                //Log.Msg("[StencilMaterialPreloader] Generating Maskable material variants for material '{0}', depth {1}", m_MaskableMaterials[i].name, 0);
                m_GeneratedMaterials[writeHead++] = StencilMaterial.Add(m_MaskableMaterials[i], stencilMask, StencilOp.Keep, CompareFunction.Equal, ColorWriteMask.All, stencilMask, 0);
            }
        }

        private void DestroyMasks() {
            if (m_GeneratedMaterials != null) {
                for (int i = 0; i < m_GeneratedMaterials.Length; i++) {
                    StencilMaterial.Remove(m_GeneratedMaterials[i]);
                    m_GeneratedMaterials[i] = null;
                }

                Log.Msg("[StencilMaterialPreloader] Destroyed {0} stencil materials!", m_GeneratedMaterials.Length);
                m_GeneratedMaterials = null;
            }
        }

        private int GetGeneratedMaterialCount() {
            int perLevel = (m_MaskMaterials.Length * 4) + m_MaskableMaterials.Length;
            return perLevel * m_MaxMaskDepth;
        }
    }
}