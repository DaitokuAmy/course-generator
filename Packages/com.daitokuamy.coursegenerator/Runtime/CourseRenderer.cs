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
        private CoursePath coursePath;
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
        [SerializeField, Tooltip("壁の幅")]
        private float _wallWidth = 1.0f;

        private Mesh _mesh;
        private List<Vector3> _vertices = new List<Vector3>();
        private List<int> _triangles = new List<int>();
        private List<Vector2> _uvs = new List<Vector2>();
        private List<Vector3> _normals = new List<Vector3>();

        private bool _dirty;
        private CoursePath _currentCoursePath;

        /// <summary>
        /// メッシュの再構築
        /// </summary>
        public void RebuildMesh() {
            _dirty = true;
        }

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
            if (_currentCoursePath != null) {
                _currentCoursePath.OnUpdatedPathEvent -= UpdatedPath;
            }

            if (coursePath != null) {
                coursePath.OnUpdatedPathEvent += UpdatedPath;
            }
            
            _currentCoursePath = coursePath;
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

            if (_width <= float.Epsilon || _unitDistance <= float.Epsilon || coursePath == null) {
                return;
            }

            _vertices.Clear();
            _triangles.Clear();
            _uvs.Clear();
            _normals.Clear();

            var totalDistance = coursePath.GetTotalDistance();

            // 床の生成
            var distance = 0.0f;
            var lineIndex = 0;
            var centerEdgeCount = Mathf.Max(0, _centerEdgeCount);
            var vtxUnitCount = centerEdgeCount + 2;
            while (distance <= totalDistance) {
                var point = coursePath.GetPointAtDistance(distance);
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
            var floorVertexCount = _vertices.Count;
            var floorIndexCount = _triangles.Count;
            var lineCount = lineIndex;

            // 壁の生成
            void CreateWall(float offset, bool reverse) {
                var vtxOffset = _vertices.Count;
                for (lineIndex = 0; lineIndex < lineCount; lineIndex++) {
                    var floorLeft = _vertices[lineIndex * vtxUnitCount];
                    var floorRight = _vertices[(lineIndex + 1) * vtxUnitCount - 1];
                    var floorUvLeft = _uvs[lineIndex * vtxUnitCount];
                    var floorNormal = _normals[lineIndex * vtxUnitCount];
                    
                    // 壁の頂点情報
                    var wallNormal = (floorRight - floorLeft).normalized;
                    var wallUpOffset = floorNormal * _wallHeight;
                    var wallLeft = floorLeft - wallNormal * offset;
                    var wallRight = floorRight + wallNormal * offset;
                    
                    // Left
                    _vertices.Add(wallLeft);
                    _normals.Add(wallNormal);
                    _uvs.Add(new Vector2(0.0f, floorUvLeft.y));
                    _vertices.Add(wallLeft + wallUpOffset);
                    _normals.Add(wallNormal);
                    _uvs.Add(new Vector2(1.0f, floorUvLeft.y));
                    
                    // Right
                    _vertices.Add(wallRight);
                    _normals.Add(-wallNormal);
                    _uvs.Add(new Vector2(0.0f, floorUvLeft.y));
                    _vertices.Add(wallRight + wallUpOffset);
                    _normals.Add(-wallNormal);
                    _uvs.Add(new Vector2(1.0f, floorUvLeft.y));

                    // Index
                    if (lineIndex > 0) {
                        var unitCount = 4;
                        var idx = (lineIndex - 1) * unitCount + vtxOffset;
                        
                        if (reverse) {
                            // Left
                            _triangles.Add(idx + 0);
                            _triangles.Add(idx + unitCount);
                            _triangles.Add(idx + 1);
                            _triangles.Add(idx + unitCount);
                            _triangles.Add(idx + unitCount + 1);
                            _triangles.Add(idx + 1);
                            
                            // Right
                            _triangles.Add(idx + 2);
                            _triangles.Add(idx + 3);
                            _triangles.Add(idx + 2 + unitCount);
                            _triangles.Add(idx + 3);
                            _triangles.Add(idx + 3 + unitCount);
                            _triangles.Add(idx + 2 + unitCount);
                        }
                        else {
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
                }
            }
            
            CreateWall(0.0f, false);
            CreateWall(_wallWidth, true);
            
            // 壁の端
            {
                var vtxOffset = _vertices.Count;
                var wallVtxUnit = lineCount * 4;
                for (lineIndex = 0; lineIndex < lineCount; lineIndex++) {
                    var wallVtxOffset = lineIndex * 4 + floorVertexCount;
                    var wallLB0 = _vertices[wallVtxOffset + 0];
                    var wallLT0 = _vertices[wallVtxOffset + 1];
                    var wallLB1 = _vertices[wallVtxOffset + 0 + wallVtxUnit];
                    var wallLT1 = _vertices[wallVtxOffset + 1 + wallVtxUnit];
                    var wallRB0 = _vertices[wallVtxOffset + 2];
                    var wallRT0 = _vertices[wallVtxOffset + 3];
                    var wallRB1 = _vertices[wallVtxOffset + 2 + wallVtxUnit];
                    var wallRT1 = _vertices[wallVtxOffset + 3 + wallVtxUnit];
                    var wallUv = _uvs[wallVtxOffset + 0];

                    var edgeNormal = (wallLT0 - wallLB0).normalized;
                    
                    // Left(Bottom/Top)
                    _vertices.Add(wallLB0);
                    _normals.Add(-edgeNormal);
                    _uvs.Add(new Vector2(1.0f, wallUv.y));
                    _vertices.Add(wallLB1);
                    _normals.Add(-edgeNormal);
                    _uvs.Add(new Vector2(0.0f, wallUv.y));
                    _vertices.Add(wallLT0);
                    _normals.Add(edgeNormal);
                    _uvs.Add(new Vector2(1.0f, wallUv.y));
                    _vertices.Add(wallLT1);
                    _normals.Add(edgeNormal);
                    _uvs.Add(new Vector2(0.0f, wallUv.y));
                    
                    // Right(Bottom/Top)
                    _vertices.Add(wallRB0);
                    _normals.Add(-edgeNormal);
                    _uvs.Add(new Vector2(0.0f, wallUv.y));
                    _vertices.Add(wallRB1);
                    _normals.Add(-edgeNormal);
                    _uvs.Add(new Vector2(1.0f, wallUv.y));
                    _vertices.Add(wallRT0);
                    _normals.Add(edgeNormal);
                    _uvs.Add(new Vector2(0.0f, wallUv.y));
                    _vertices.Add(wallRT1);
                    _normals.Add(edgeNormal);
                    _uvs.Add(new Vector2(1.0f, wallUv.y));

                    // Index
                    if (lineIndex > 0) {
                        var unitCount = 8;
                        var idx = (lineIndex - 1) * unitCount + vtxOffset;
                        
                        // Left(Bottom/Top)
                        _triangles.Add(idx + 0);
                        _triangles.Add(idx + unitCount);
                        _triangles.Add(idx + 1);
                        _triangles.Add(idx + unitCount);
                        _triangles.Add(idx + unitCount + 1);
                        _triangles.Add(idx + 1);
                        _triangles.Add(idx + 2);
                        _triangles.Add(idx + 3);
                        _triangles.Add(idx + unitCount + 2);
                        _triangles.Add(idx + unitCount + 2);
                        _triangles.Add(idx + 3);
                        _triangles.Add(idx + unitCount + 3);
                        
                        // Right(Bottom/Top)
                        _triangles.Add(idx + 6);
                        _triangles.Add(idx + 7);
                        _triangles.Add(idx + unitCount + 6);
                        _triangles.Add(idx + unitCount + 6);
                        _triangles.Add(idx + 7);
                        _triangles.Add(idx + unitCount + 7);
                        _triangles.Add(idx + 4);
                        _triangles.Add(idx + unitCount + 4);
                        _triangles.Add(idx + 5);
                        _triangles.Add(idx + unitCount + 4);
                        _triangles.Add(idx + unitCount + 5);
                        _triangles.Add(idx + 5);
                    }
                }
            }
            
            // 始点/終点
            {
                var wallVtxUnit = lineCount * 4;
                
                void CreatePoint(int wallVtxOffset, bool reverse) {
                    var vtxOffset = _vertices.Count;
                    var wallLB0 = _vertices[wallVtxOffset + 0];
                    var wallLT0 = _vertices[wallVtxOffset + 1];
                    var wallLB1 = _vertices[wallVtxOffset + 0 + wallVtxUnit];
                    var wallLT1 = _vertices[wallVtxOffset + 1 + wallVtxUnit];
                    var wallRB0 = _vertices[wallVtxOffset + 2];
                    var wallRT0 = _vertices[wallVtxOffset + 3];
                    var wallRB1 = _vertices[wallVtxOffset + 2 + wallVtxUnit];
                    var wallRT1 = _vertices[wallVtxOffset + 3 + wallVtxUnit];

                    var pointNormal = Vector3.Cross( wallLB1 - wallLB0, wallLT0 - wallLB0);
                    pointNormal = reverse ? -pointNormal : pointNormal;
                    
                    // Left(Bottom/Top)
                    _vertices.Add(wallLB0);
                    _normals.Add(pointNormal);
                    _uvs.Add(new Vector2(1.0f, 0.0f));
                    _vertices.Add(wallLB1);
                    _normals.Add(pointNormal);
                    _uvs.Add(new Vector2(0.0f, 0.0f));
                    _vertices.Add(wallLT0);
                    _normals.Add(pointNormal);
                    _uvs.Add(new Vector2(1.0f, 1.0f));
                    _vertices.Add(wallLT1);
                    _normals.Add(pointNormal);
                    _uvs.Add(new Vector2(0.0f, 1.0f));
                    
                    // Right(Bottom/Top)
                    _vertices.Add(wallRB0);
                    _normals.Add(pointNormal);
                    _uvs.Add(new Vector2(0.0f, 0.0f));
                    _vertices.Add(wallRB1);
                    _normals.Add(pointNormal);
                    _uvs.Add(new Vector2(1.0f, 0.0f));
                    _vertices.Add(wallRT0);
                    _normals.Add(pointNormal);
                    _uvs.Add(new Vector2(0.0f, 1.0f));
                    _vertices.Add(wallRT1);
                    _normals.Add(pointNormal);
                    _uvs.Add(new Vector2(1.0f, 1.0f));

                    // Index
                    var idx = vtxOffset;

                    if (reverse) {
                        // Left(Bottom/Top)
                        _triangles.Add(idx + 0);
                        _triangles.Add(idx + 2);
                        _triangles.Add(idx + 1);
                        _triangles.Add(idx + 2);
                        _triangles.Add(idx + 3);
                        _triangles.Add(idx + 1);
                    
                        // Right(Bottom/Top)
                        _triangles.Add(idx + 4);
                        _triangles.Add(idx + 5);
                        _triangles.Add(idx + 6);
                        _triangles.Add(idx + 5);
                        _triangles.Add(idx + 7);
                        _triangles.Add(idx + 6);
                    }
                    else {
                        // Left(Bottom/Top)
                        _triangles.Add(idx + 0);
                        _triangles.Add(idx + 1);
                        _triangles.Add(idx + 2);
                        _triangles.Add(idx + 2);
                        _triangles.Add(idx + 1);
                        _triangles.Add(idx + 3);
                    
                        // Right(Bottom/Top)
                        _triangles.Add(idx + 4);
                        _triangles.Add(idx + 6);
                        _triangles.Add(idx + 5);
                        _triangles.Add(idx + 5);
                        _triangles.Add(idx + 6);
                        _triangles.Add(idx + 7);
                    }
                }

                CreatePoint(floorVertexCount, false);
                CreatePoint((lineCount - 1) * 4 + floorVertexCount, true);
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