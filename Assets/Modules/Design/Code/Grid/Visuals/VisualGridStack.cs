using BeauRoutine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Design.Visuals
{
    public class VisualGridStack
    {
        public Dimensions LayerDims; // x and y dims of each layer
        [NonSerialized] public VisualGridLayer[] GridLayers; // layers ordered from highest to lowest
    }

    public static class VisualGridStackUtility
    {
        private static float OUTLINE_WIDTH = 0.03f;

        public static void Init(ref VisualGridStack visualGridStack, int xDim, int yDim, GameObject cellVisualsPrefab, Transform container)
        {
            visualGridStack.LayerDims.X = xDim;
            visualGridStack.LayerDims.Y = yDim;

            visualGridStack.GridLayers = new VisualGridLayer[]
            {
                new VisualGridLayer(visualGridStack.LayerDims.X, visualGridStack.LayerDims.Y, (int)StackLayer.Metal, cellVisualsPrefab, container),  // metal layer (highest)
                new VisualGridLayer(visualGridStack.LayerDims.X, visualGridStack.LayerDims.Y, (int)StackLayer.Transistor, cellVisualsPrefab, container)   // transistor layer (lowest)
            };
        }

        public static void Destroy(ref VisualGridStack visualGridStack)
        {
            visualGridStack.GridLayers[(int)StackLayer.Metal].Destroy();
            visualGridStack.GridLayers[(int)StackLayer.Transistor].Destroy();
        }

        public static void RefreshGridSize(SpriteRenderer gridRenderer, SpriteRenderer gridOutline, int xDim, int yDim)
        {
            gridRenderer.size = new Vector2(xDim + OUTLINE_WIDTH, yDim + OUTLINE_WIDTH);
            gridOutline.size = new Vector2(xDim + OUTLINE_WIDTH + 0.1f, yDim + OUTLINE_WIDTH + 0.1f);
        }

        public static void SetOutlineVisible(VisualGridStackState visualState, bool active) {
            if (active) {
                visualState.GridTestOutline.SetAlpha(1f);
            } else {
                visualState.GridTestOutline.SetAlpha(0f);
            }
        }
    }
}
