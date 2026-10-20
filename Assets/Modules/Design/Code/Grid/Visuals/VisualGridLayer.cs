using FieldDay;
using SpaceFab.Design.Visuals;
using UnityEngine;

namespace SpaceFab.Design
{
    public class VisualGridLayer
    {
        public Dimensions Dimensions;
        public int LayerIndex;
        private VisualGridCell[] m_Cells; // accessed in row, col order

        #region Constructor & Cleanup

        public VisualGridLayer(int xDim, int yDim, int layerIndex, GameObject cellVisualsPrefab, Transform container)
        {
            Dimensions = new Dimensions(xDim, yDim);
            LayerIndex = layerIndex;
            m_Cells = new VisualGridCell[yDim * xDim];
            float cellOffset = 0.5f;
            float xOffset = (DesignConsts.NUM_GRID_COLS - xDim) / 2;
            float yOffset = (DesignConsts.NUM_GRID_ROWS - yDim) / 2;
            float layerShift = 0;
            if (layerIndex == (int) StackLayer.Metal) {
                layerShift += VisualGridCellUtility.METAL_OFFSET;
            } else if (layerIndex == (int) StackLayer.Transistor) {
                layerShift += VisualGridCellUtility.SEMI_OFFSET;
            }
            for (int row = 0; row < yDim; row++)
            {
                for (int col = 0; col < xDim; col++)
                {
                    var cell = GameObject.Instantiate(cellVisualsPrefab, container).GetComponent<VisualGridCell>();
                    cell.transform.localPosition = new Vector3(col + cellOffset + xOffset, row + cellOffset + yOffset, 0);
                    cell.gameObject.name = "Cell Visual (" + col + ", " + row + ", " + layerIndex + ")";
                    SetCell(col, row, cell);

                    if (layerShift != 0) {
                        foreach(var topLayer in cell.ShiftForMetalLayer) {
                            topLayer.Translate(0, layerShift, 0, Space.Self);
                        }
                    }
                }
            }
        }

        public void Destroy()
        {
            for (int row = 0; row < Dimensions.Y; row++)
            {
                for (int col = 0; col < Dimensions.X; col++)
                {
                    GameObject.Destroy(m_Cells[row * Dimensions.X + col]);
                }
            }
        }

        #endregion // Constructor & Cleanup

        #region Gets & Sets

        // Access x, y in row, col order
        public VisualGridCell GetCell(int x, int y)
        {
            return m_Cells[y * Dimensions.X + x];
        }

        // Set cell at x, y in row, col order
        public void SetCell(int x, int y, VisualGridCell cell)
        {
            m_Cells[y * Dimensions.X + x] = cell;
        }

        // Set cell at x, y in row, col order
        public void SetCell(Vector2Int coord, VisualGridCell cell)
        {
            m_Cells[coord.y * Dimensions.X + coord.x] = cell;
        }

        #endregion // Gets & Sets

        #region Refresh

        public void RefreshAll(GridSpriteDB spriteDB)
        {
            GridLayer layer = Find.State<GridStackState>().GridStack.GridLayers[LayerIndex];

            // Per-cell flow + temp-transform reads go through SimulateRunScratch now. Grab it
            // once up front — may be null if Simulate mode was never entered this session, in
            // which case VisualGridCellUtility defaults to empty flow + no temp-transform.
            SimulateRunScratch scratch = Find.State<SimulateRunScratch>();
            int cellsPerLayer = Dimensions.X * Dimensions.Y;

            // Cell by cell, update renderer with data
            for (int row = 0; row < Dimensions.Y; row++)
            {
                for (int col = 0; col < Dimensions.X; col++)
                {
                    var cell = GridLayerUtility.GetCell(layer, col, row);
                    int cellIndex = SimulateRunScratchUtility.CellIndex(LayerIndex, col, row, Dimensions.X, cellsPerLayer);
                    VisualGridCellUtility.RefreshVisual(ref m_Cells[row * Dimensions.X + col], cell, scratch, cellIndex, LayerIndex, col, row, spriteDB);
                }
            }
        }

        #endregion // Refresh
    }
}
