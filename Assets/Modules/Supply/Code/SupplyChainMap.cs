using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Assets;
using FieldDay.Scenes;
using FieldDay.SharedState;
using ScriptableBake;
using SpaceFab.Materials;
using SpaceFab.Research;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

namespace SpaceFab.Supply
{
    public class SupplyChainMap : SharedStateComponent, IBaked, IScenePreload
    {
        public SupplyChainMapData[] Entries;

        public Transform NodeRoot;
        public SupplyRouteNode[] Nodes;
        public SupplyRouteHazard[] Hazards;

        [NonSerialized] public SupplyRouteNode Home;
        [NonSerialized] public int NodeCount;
        [NonSerialized] public int HazardCount;

#if UNITY_EDITOR

        int IBaked.Order { get { return 1000; } }

        bool IBaked.Bake(BakeFlags flags, BakeContext context) {
            Nodes = NodeRoot.GetComponentsInChildren<SupplyRouteNode>(true);
            Hazards = NodeRoot.GetComponentsInChildren<SupplyRouteHazard>(true);
            return true;
        }

#endif // UNITY_EDITOR

        IEnumerator<WorkSlicer.Result?> IScenePreload.Preload() {
            for(int i = 0; i < Nodes.Length; i++) {
                var node = Nodes[i];
                node.Id = node.name;
                node.Index = i;

                if (node.Type != SupplyRouteNodeType.Home && node.InfoPopup != null) {
                    MaterialAsset matView = Find.NamedAsset<MaterialAsset>(node.MaterialType);
                    node.InfoPopup.OutputMaterialIcon.sprite = matView.GemSprite;
                    if (node.Type == SupplyRouteNodeType.Converter) {
                        matView = Find.NamedAsset<MaterialAsset>(node.ConversionInputType);
                        node.InfoPopup.InputMaterialIcon.sprite = matView.GemSprite;
                    }
                }

                SupplyRouteUtility.InitializeTooltipReferences(node);
                Find.State(out PlayerProgressState progressState, out ChapterState chapterState);
                SupplyRouteUtility.RefreshNodeTooltip(node, progressState, chapterState.ChapterDefinition);
                node.gameObject.SetActive(false);
                yield return null;
            }

            for (int i = 0; i < Hazards.Length; i++) {
                var hazard = Hazards[i];
                hazard.Id = hazard.name;
                hazard.Index = i;

                hazard.gameObject.SetActive(false);
                yield return null;
            }
        }

#if UNITY_EDITOR
        [CustomEditor(typeof(SupplyChainMap))]
        private sealed class Inspector : Editor {
            public SupplyChainMapData MapData;

            public override void OnInspectorGUI() {
                base.OnInspectorGUI();

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("-- EDITING --", EditorStyles.boldLabel);

                SupplyChainMap map = (SupplyChainMap)target;

                MapData = (SupplyChainMapData) EditorGUILayout.ObjectField("Edit Map", MapData, typeof(SupplyChainMapData), false);
                if (MapData != null) {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Load Data")) {
                        EditorImport(MapData, map.NodeRoot);
                    }
                    if (GUILayout.Button("Save Data")) {
                        EditorExport(map.NodeRoot, MapData);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        static private void EditorExport(Transform root, SupplyChainMapData target) {
            List<SupplyChainMapData.NodeData> nodes = new List<SupplyChainMapData.NodeData>();
            List<SupplyChainMapData.NodeOverride> overrides = new List<SupplyChainMapData.NodeOverride>();
            List<SupplyChainMapData.NodeData> hazards = new List<SupplyChainMapData.NodeData>();
            Rect cameraBounds = new Rect(-20, -20, 40, 40);

            int childCount = root.childCount;
            for(int i = 0; i < childCount; i++) {
                Transform child = root.GetChild(i);
                if (!child.gameObject.activeSelf) {
                    continue;
                }

                if (child.TryGetComponent(out SupplyRouteNode node)) {
                    Vector2 position = node.transform.localPosition;
                    nodes.Add(new SupplyChainMapData.NodeData() {
                        Name = node.gameObject.name,
                        Position = position
                    });

                    if (HasNodeOverrides(node)) {
                        overrides.Add(new SupplyChainMapData.NodeOverride() {
                            Name = node.gameObject.name,
                            Time = node.Time,
                            Cost = node.Cost,
                            Risk = node.Risk
                        });
                    }

                } else if (child.TryGetComponent(out SupplyRouteHazard hazard)) {
                    Vector2 position = hazard.transform.localPosition;
                    hazards.Add(new SupplyChainMapData.NodeData() {
                        Name = hazard.gameObject.name,
                        Position = position
                    });
                } else if (child.TryGetComponent(out BoxCollider2D collider)) {
                    cameraBounds = Geom.BoundsToRect(collider.bounds);
                }
            }

            Baking.PrepareUndo(target, "Exporting map data");
            target.Positions = nodes.ToArray();
            target.Overrides = overrides.ToArray();
            target.Hazards = hazards.ToArray();
            target.CameraBounds = cameraBounds;

            Log.Msg("[SupplyChainMap] Exported map data ({0} nodes) to '{1}'", target.Positions.Length, target.name);
        }

        static private bool HasNodeOverrides(SupplyRouteNode node) {
            var prefabOverrides = PrefabUtility.GetObjectOverrides(node.gameObject);
            foreach(var overrideDesc in prefabOverrides) {
                if (overrideDesc.instanceObject == node) {
                    return true;
                }
            }
            return false;
        }

        static private void EditorImport(SupplyChainMapData data, Transform root) {
            int childCount = root.childCount;
            for(int i = 0; i < childCount; i++) {
                Transform child = root.GetChild(i);

                if (child.TryGetComponent(out SupplyRouteNode node)) {
                    Baking.PrepareUndo(child, "Importing map data");

                    PrefabUtility.RevertObjectOverride(node, InteractionMode.UserAction);

                    StringHash32 id = node.gameObject.name;
                    bool activeState = false;
                    foreach (var pos in data.Positions) {
                        if (pos.Name == id) {
                            child.localPosition = pos.Position;
                            activeState = true;
                            break;
                        }
                    }

                    foreach (var change in data.Overrides) {
                        if (change.Name == id) {
                            node.Time = change.Time;
                            node.Cost = change.Cost;
                            node.Risk = change.Risk;
                            break;
                        }
                    }

                    child.gameObject.SetActive(activeState);
                    Baking.SetDirty(child);
                } else if (child.TryGetComponent(out SupplyRouteHazard hazard)) {
                    Baking.PrepareUndo(child, "Importing map data");

                    PrefabUtility.RevertObjectOverride(hazard, InteractionMode.UserAction);

                    StringHash32 id = hazard.gameObject.name;
                    bool activeState = false;
                    foreach (var pos in data.Hazards) {
                        if (pos.Name == id) {
                            child.localPosition = pos.Position;
                            activeState = true;
                            break;
                        }
                    }

                    child.gameObject.SetActive(activeState);
                    Baking.SetDirty(child);
                } else if (child.TryGetComponent(out BoxCollider2D collider)) {
                    Baking.PrepareUndo(child, "Importing map data");

                    Vector2 center = data.CameraBounds.center;
                    Vector2 size = data.CameraBounds.size;

                    collider.transform.localPosition = center;
                    collider.offset = default;
                    collider.size = size;

                    Baking.SetDirty(child);
                }
            }

            
        }
#endif // UNITY_EDITOR
    }

    static public partial class SupplyRouteUtility {
        static public SupplyRouteNode GetNodeForIndex(int index) {
            Find.State(out SupplyChainMap loader);
            Assert.True(index >= 0 && index < loader.Nodes.Length, "Supply node index {0} out of range", index);
            return loader.Nodes[index];
        }

        static public SupplyRouteNode GetNodeForId(StringHash32 id) {
            if (id.IsEmpty) {
                return null;
            }

            Find.State(out SupplyChainMap loader);
            foreach(var node in loader.Nodes) {
                if (node.Id == id) {
                    return node;
                }
            }

            Assert.Fail("No supply node with id '{0}'", id);
            return null;
        }

        // Non-asserting counterpart to GetNodeForId, for resolving ids that came from outside the
        // current scene - a saved route may name a node that was renamed or removed since.
        public static bool TryGetNodeForId(SupplyChainMap map, StringHash32 id, out SupplyRouteNode node) {
            if (!id.IsEmpty) {
                foreach (var mapNode in map.Nodes) {
                    if (mapNode.Id == id) {
                        node = mapNode;
                        return true;
                    }
                }
            }

            node = null;
            return false;
        }

        static public SupplyRouteHazard GetHazardForIndex(int index) {
            Find.State(out SupplyChainMap loader);
            Assert.True(index >= 0 && index < loader.Hazards.Length, "Supply hazard index {0} out of range", index);
            return loader.Hazards[index];
        }

        static public SupplyRouteHazard GetHazardForId(StringHash32 id) {
            if (id.IsEmpty) {
                return null;
            }

            Find.State(out SupplyChainMap loader);
            foreach (var hazard in loader.Hazards) {
                if (hazard.Id == id) {
                    return hazard;
                }
            }

            Assert.Fail("No supply hazard with id '{0}'", id);
            return null;
        }
    }
}
