using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

namespace CourseGenerator {
    /// <summary>
    /// コース用のパス
    /// </summary>
    [ExecuteAlways]
    public class CourseRenderer : MonoBehaviour {
        [SerializeField, Tooltip("描画対象のPath")]
        private Path _path;
        [SerializeField, Tooltip("床描画に使うMaterial")]
        private Material _floorMaterial;
        [SerializeField, Tooltip("壁描画に使うMaterial")]
        private Material _wallMaterial;
        [SerializeField, Tooltip("ポリゴン分解する単位距離")]
        private float _unitDistance = 1.0f;
        [SerializeField, Tooltip("中央の分割数")]
        private int _centerEdgeCount = 0;
        [SerializeField, Tooltip("コースの幅")]
        private float _width = 1.0f;
        [SerializeField, Tooltip("壁の高さ")]
        private float _wallHeight = 1.0f;

        private Mesh _mesh;
        private List<Vector3> _vertices = new List<Vector3>();
        private List<int> _triangles = new List<int>();
        private List<Vector2> _uvs = new List<Vector2>();
        private List<Vector3> _normals = new List<Vector3>();

        private bool _dirty;
        private Path _currentPath;

        /// <summary>
        /// 生成時処理
        /// </summary>
        private void Awake() {
            _dirty = true;
        }

        /// <summary>
        /// 後更新処理
        /// </summary>
        private void LateUpdate() {
            if (_dirty) {
                GenerateMesh();
                _dirty = false;
            }

            if (_mesh != null) {
                if (_floorMaterial != null) {
                    Graphics.DrawMesh(_mesh, Vector3.zero, Quaternion.identity, _floorMaterial, gameObject.layer, null, 0);
                }
                if (_wallMaterial != null) {
                    Graphics.DrawMesh(_mesh, Vector3.zero, Quaternion.identity, _wallMaterial, gameObject.layer, null, 1);
                }
            }
        }

        /// <summary>
        /// 値変化時処理
        /// </summary>
        private void OnValidate() {
            _dirty = true;

            void UpdatedPath() {
                _dirty = true;
            }

            // Pathの更新があった時、Dirtyを立てるようにイベント監視する
            if (_currentPath != null) {
                _currentPath.OnUpdatedPathEvent -= UpdatedPath;
            }

            if (_path != null) {
                _path.OnUpdatedPathEvent += UpdatedPath;
            }
            
            _currentPath = _path;
        }

        /// <summary>
        /// メッシュの生成
        /// </summary>
        private void GenerateMesh() {
            if (_mesh != null) {
                DestroyImmediate(_mesh);
                _mesh = null;
            }
            
            _mesh = new Mesh();

            if (_width <= float.Epsilon || _unitDistance <= float.Epsilon || _path == null) {
                return;
            }

            _vertices.Clear();
            _triangles.Clear();
            _uvs.Clear();
            _normals.Clear();

            var totalDistance = _path.GetTotalDistance();

            // 床の生成
            var distance = 0.0f;
            var lineIndex = 0;
            var centerEdgeCount = Mathf.Max(0, _centerEdgeCount);
            var vtxUnitCount = centerEdgeCount + 2;
            while (distance <= totalDistance) {
                var point = _path.GetPointAtDistance(distance);
                var halfWidth = _width * 0.5f;
                var left = point.Position - point.Right * halfWidth;
                var right = point.Position + point.Right * halfWidth;

                // Left
                _vertices.Add(left);
                _uvs.Add(new Vector2(0.0f, distance));
                _normals.Add(point.Normal);

                // Center
                for (var i = 0; i < centerEdgeCount; i++) {
                    var rate = (i + 1) / (float)(centerEdgeCount + 1);
                    var pos = Vector3.Lerp(left, right, rate);
                    _vertices.Add(pos);
                    _uvs.Add(new Vector2(rate, distance));
                    _normals.Add(point.Normal);
                }

                // Right
                _vertices.Add(right);
                _uvs.Add(new Vector2(1.0f, distance));
                _normals.Add(point.Normal);

                // Index
                if (lineIndex > 0) {
                    var idx = (lineIndex - 1) * vtxUnitCount;
                    for (var i = 0; i < centerEdgeCount + 1; i++) {
                        _triangles.Add(idx + i + 0);
                        _triangles.Add(idx + i + vtxUnitCount);
                        _triangles.Add(idx + i + 1);
                        _triangles.Add(idx + i + 1);
                        _triangles.Add(idx + i + vtxUnitCount);
                        _triangles.Add(idx + i + 1 + vtxUnitCount);
                    }
                }

                distance += _unitDistance;
                lineIndex++;
            }
            
            // FloorのIndex数記憶
            var floorIndexCount = _triangles.Count;
            
            // 壁の生成
            var vtxOffset = _vertices.Count;
            var lineCount = lineIndex;
            for (lineIndex = 0; lineIndex < lineCount; lineIndex++) {
                var floorLeft = _vertices[lineIndex * vtxUnitCount];
                var floorRight = _vertices[(lineIndex + 1) * vtxUnitCount - 1];
                var floorUvLeft = _uvs[lineIndex * vtxUnitCount];
                var floorNormal = _normals[lineIndex * vtxUnitCount];
                
                // 壁の上方向
                var wallUpOffset = floorNormal * _wallHeight;
                
                // WorldUpに垂直なNormalを求める
                var normal = floorRight - floorLeft;
                normal.y = 0.0f;
                normal.Normalize();
                
                // Left
                _vertices.Add(floorLeft);
                _normals.Add(normal);
                _uvs.Add(new Vector2(0.0f, floorUvLeft.y));
                _vertices.Add(floorLeft + wallUpOffset);
                _normals.Add(normal);
                _uvs.Add(new Vector2(1.0f, floorUvLeft.y));
                
                // Right
                _vertices.Add(floorRight);
                _normals.Add(-normal);
                _uvs.Add(new Vector2(0.0f, floorUvLeft.y));
                _vertices.Add(floorRight + wallUpOffset);
                _normals.Add(-normal);
                _uvs.Add(new Vector2(1.0f, floorUvLeft.y));

                // Index
                if (lineIndex > 0) {
                    var unitCount = 4;
                    var idx = (lineIndex - 1) * unitCount + vtxOffset;
                    
                    // Left
                    _triangles.Add(idx + 0);
                    _triangles.Add(idx + 1);
                    _triangles.Add(idx + unitCount);
                    _triangles.Add(idx + unitCount);
                    _triangles.Add(idx + 1);
                    _triangles.Add(idx + unitCount + 1);
                    
                    // Right
                    _triangles.Add(idx + 2);
                    _triangles.Add(idx + 2 + unitCount);
                    _triangles.Add(idx + 3);
                    _triangles.Add(idx + 3);
                    _triangles.Add(idx + 2 + unitCount);
                    _triangles.Add(idx + 3 + unitCount);
                }
            }

            // Mesh構築
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetNormals(_normals);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();
            
            // SubMesh分離
            _mesh.subMeshCount = 2;
            _mesh.SetSubMesh(0, new SubMeshDescriptor(0, floorIndexCount));
            _mesh.SetSubMesh(1, new SubMeshDescriptor(floorIndexCount, _triangles.Count - floorIndexCount));
            
            // SystemRAMから解放
            _mesh.UploadMeshData(true);
        }
    }
}