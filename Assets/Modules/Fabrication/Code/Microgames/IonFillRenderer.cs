using FieldDay;
using FieldDay.Rendering;
using UnityEngine;

namespace SpaceFab.Fabrication.Microgames
{
    /// <summary>
    /// Rendering class for ion points making use of instance buffer
    /// </summary>
    public class IonFillRenderer : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D m_BoxCollider;

        private int m_Rows, m_Columns;
        private float m_CellWidth, m_CellHeight;
        private float m_FillRadius;

        private int m_TotalFilledPoints;
        
        public Mesh IonMesh;
        public Material IonMaterial;

        public bool IsRendering = false;

        public struct IonPoint
        {
            public Vector2 Position;
            public Matrix4x4 Matrix;
            public bool IsFilled;
        }

        private IonPoint[] IonPoints;

        public int Setup(float density, float fillRadius)
        {
            m_FillRadius = fillRadius;

            Vector2 bottomLeft = m_BoxCollider.bounds.min;
            Vector2 topRight = m_BoxCollider.bounds.max;
            float width = topRight.x - bottomLeft.x;
            float height = topRight.y - bottomLeft.y;
        
            m_Columns = Mathf.FloorToInt(width * density);
            m_Rows = Mathf.FloorToInt(height * density);

            // these should be the same but just in case
            m_CellWidth = width / m_Columns;
            m_CellHeight = height / m_Rows;
            float halfWidth = m_CellWidth * 0.5f;
            float halfHeight = m_CellHeight * 0.5f;

            Vector3 cellScale = new Vector3(m_CellWidth, m_CellHeight, 1);
            Quaternion cellRotation = Quaternion.identity;
            
            IonPoints = new IonPoint[m_Rows * m_Columns];

            for (int y = 0; y < m_Rows; y++)
            {
                for (int x = 0; x < m_Columns; x++)
                {
                    Vector3 cellCenter = new Vector3(
                        bottomLeft.x + (x * m_CellWidth) + halfWidth,
                        bottomLeft.y + (y * m_CellHeight) + halfHeight,
                        -0.1f // place slightly towards camera to fix some rendering issues?
                    );

                    IonPoints[y * m_Columns + x] = new IonPoint()
                    {
                        Position = cellCenter,
                        Matrix = Matrix4x4.TRS(cellCenter, cellRotation, cellScale),
                        IsFilled = false
                    };
                }
            }

            return IonPoints.Length; // for IonPatternData to use in determining when all grids are filled
        }

        public int ProcessWork()
        {
            if (Game.Input.IsMouseDown(0))
            {
                Vector2 mousePosition = Game.Rendering.PrimaryCamera.ScreenToWorldPoint(Input.mousePosition);
                RaycastHit2D hit = Physics2D.Raycast(mousePosition, Vector2.zero);

                if (hit.collider != null && hit.collider == m_BoxCollider)
                {
                    // this is a direct lookup per point, which is less expensive
                    // Vector2 bottomLeft = hit.collider.bounds.min;
                    // Vector2 localClick = mousePosition - bottomLeft;

                    // int xPosition = Mathf.FloorToInt(localClick.x / m_CellWidth);
                    // int yPosition = Mathf.FloorToInt(localClick.y / m_CellHeight);

                    // int positionalIndex = yPosition * m_Columns + xPosition;    
                    // if (positionalIndex >= 0 && positionalIndex < IonPoints.Length && !IonPoints[positionalIndex].IsFilled)
                    // {
                    //     IonPoints[positionalIndex].IsFilled = true;
                    //     m_TotalFilledPoints++;
                    // }
                    
                    // this iterates over all with a distance check, which may be too much? Unsure
                    for (int i = 0; i < IonPoints.Length; i++)
                    {
                        if (!IonPoints[i].IsFilled && Vector2.Distance(IonPoints[i].Position, mousePosition) <= m_FillRadius)
                        {
                            IonPoints[i].IsFilled = true;
                            m_TotalFilledPoints++;
                        }
                    }
                }
            }

            return m_TotalFilledPoints; // use for counting
        }

        void LateUpdate()
        {
            if (IsRendering) PerformRendering();
        }

        public unsafe void PerformRendering()
        {
            DefaultInstancedMeshParams* paramBuffer = stackalloc DefaultInstancedMeshParams[512]; 
            RenderParams renderParams = new RenderParams(IonMaterial);
            renderParams.layer = LayerMasks.Default_Index;
            Mesh mesh = IonMesh;
            var instanceHelper = new InstancedMeshBuffer<DefaultInstancedMeshParams>(paramBuffer, 512, renderParams, mesh);
            
            for (int i = 0; i < IonPoints.Length; i++)
            {
                if (IonPoints[i].IsFilled)
                {
                    DefaultInstancedMeshParams instParams = default;
                    instParams.objectToWorld = IonPoints[i].Matrix;
                    instanceHelper.Queue(instParams);
                }
            }

            instanceHelper.Flush();
            instanceHelper.Dispose();
        }
    }
}