using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CourseGenerator.Editor {
    /// <summary>
    /// CoursePathのEditor拡張
    /// </summary>
    [CustomEditor(typeof(CoursePath))]
    public class CoursePathEditor : UnityEditor.Editor {
        private const float NormalDistance = 0.5f;
        private const float PointSize = 0.5f;
        private const float WindowWidth = 300.0f;

        private static readonly Color PathColor = new(1, 1, 1);
        private static readonly Color SelectedPathColor = new(1, 1, 0);
        private static readonly Color PointColor = new(0.75f, 0.75f, 1);
        private static readonly Color SelectedPointColor = new(1, 0.5f, 0.5f);
        private static readonly Color NormalColor = new(0, 1, 0);
        
        // 選択情報
        private class SelectedInfo {
            public CoursePath.PathNode Node;
            public int Index;
            public Vector2 Scroll;
        }
        
        private readonly Dictionary<CoursePath.PathNode, int> _nodeToControlIds = new Dictionary<CoursePath.PathNode, int>();
        private readonly Dictionary<int, CoursePath.PathNode> _controlIdToNodes = new Dictionary<int, CoursePath.PathNode>();
        private SelectedInfo _selectedInfo;

        /// <summary>
        /// シーンGUI描画
        /// </summary>
        private void OnSceneGUI() {
            var path = (CoursePath)target;
            var transform = path.transform;
            
            var prevColor = Handles.color;
            
            // 始点の初期化
            var trans = transform;
            var startPoint = new CoursePath.Point {
                Position = trans.position,
                Forward = trans.forward,
                Right = trans.right,
                Normal = trans.up
            };
            
            // PathNodeの描画
            for (var i = 0; i < path.PathNodes.Count; i++) {
                startPoint = DrawPathNodeGizmos(path, startPoint, path.PathNodes[i]);
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0) {
                var controlId = HandleUtility.nearestControl;
                if (_controlIdToNodes.TryGetValue(controlId, out var node)) {
                    _selectedInfo = new SelectedInfo {
                        Node = node,
                        Index = path.PathNodes.ToList().IndexOf(node),
                        Scroll = Vector2.zero
                    };
                }
            }

            if (Event.current.type == EventType.ExecuteCommand) {
                if (Event.current.commandName == "FrameSelected") {
                    if (_selectedInfo != null) {
                        var point = path.GetPoint(_selectedInfo.Index);
                        SceneView.lastActiveSceneView.LookAt(point.Position);
                        Event.current.Use();
                    }
                }
            }
            
            // GUI描画
            if (_selectedInfo != null) {
                var prop = serializedObject.FindProperty("_pathNodes").GetArrayElementAtIndex(_selectedInfo.Index);
                var width = WindowWidth;
                var height = 44.0f;
                var viewSize = SceneView.lastActiveSceneView.position.size;
                foreach (SerializedProperty childProp in prop) {
                    height += EditorGUI.GetPropertyHeight(childProp, true);
                }
                var rect = new Rect(viewSize.x - width - 5, viewSize.y - height - 50, width, height);
                GUILayout.Window(0, rect, _ => {
                    serializedObject.Update();
                    
                    var pathNodeProp = serializedObject.FindProperty("_pathNodes").GetArrayElementAtIndex(_selectedInfo.Index);
                    using (var scope = new EditorGUILayout.ScrollViewScope(_selectedInfo.Scroll, "Box")) {
                        foreach (SerializedProperty childProp in pathNodeProp) {
                            EditorGUILayout.PropertyField(childProp, true);
                        }
                        _selectedInfo.Scroll = scope.scrollPosition;
                    }

                    serializedObject.ApplyModifiedProperties();
                
                    switch (Event.current.type) {
                        case EventType.MouseDown:
                        case EventType.MouseUp:
                        case EventType.MouseDrag:
                            Event.current.Use();
                            break;
                    }
                }, $"PathNode[{_selectedInfo.Index}]");
            }

            Handles.color = prevColor;
        }

        /// <summary>
        /// PathNode毎のGizmo描画
        /// </summary>
        private CoursePath.Point DrawPathNodeGizmos(CoursePath path, CoursePath.Point startPoint, CoursePath.PathNode node) {
            // ライン描画
            var splitDistance = 1.0f;
            var lineCount = (int)(path.GetDistance(node) / splitDistance) + 1;
            var totalDistance = 0.0f;
            var selected = _selectedInfo != null && _selectedInfo.Node == node;
            for (var i = 0; i < lineCount; i++) {
                var distance = totalDistance;
                totalDistance += splitDistance;
                var nextDistance = totalDistance;
                var point = path.GetPointAtDistance(startPoint, node, distance);
                var nextPoint = path.GetPointAtDistance(startPoint, node, nextDistance);
                
                // パス
                Handles.color = selected ? SelectedPathColor : PathColor;
                Handles.DrawLine(point.Position, nextPoint.Position);
                
                if (i > 0) {
                    // 法線
                    Handles.color = NormalColor;
                    Handles.DrawLine(point.Position, point.Position + point.Normal * NormalDistance);
                }
            }

            var endPoint = path.GetPoint(startPoint, node, 1.0f);
            
            // 終点描画
            Handles.color = selected ? SelectedPointColor : PointColor;
            Handles.SphereHandleCap(GetControlId(node), startPoint.Position, Quaternion.identity, PointSize, Event.current.type);
            Handles.color = NormalColor;
            Handles.DrawLine(startPoint.Position, startPoint.Position + startPoint.Normal * NormalDistance);
            
            return endPoint;
        }

        /// <summary>
        /// ControlIdの取得
        /// </summary>
        private int GetControlId(CoursePath.PathNode node) {
            if (node == null) {
                return 0;
            }
            
            if (_nodeToControlIds.TryGetValue(node, out var controlId)) {
                return controlId;
            }

            controlId = GUIUtility.GetControlID(FocusType.Passive);
            _nodeToControlIds[node] = controlId;
            _controlIdToNodes[controlId] = node;
            return controlId;
        }
    }
}