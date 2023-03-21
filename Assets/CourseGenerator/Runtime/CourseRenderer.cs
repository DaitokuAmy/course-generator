using System.Collections.Generic;
using UnityEngine;

namespace CourseGenerator {
    /// <summary>
    /// コース用のパス
    /// </summary>
    [ExecuteAlways]
    public class CourseRenderer : MonoBehaviour {
        [SerializeField, Tooltip("描画対象のPath")]
        private Path _path;
        [SerializeField, Tooltip("描画に使うMaterial")]
        private Material _material;
        [SerializeField, Tooltip("ポリゴン分解する単位距離")]
        private float _unitDistance = 1.0f;
        [SerializeField, Tooltip("コースの幅")]
        private float _width = 1.0f;

        private Mesh _mesh;
        private List<Vector3> _vertices = new List<Vector3>();
        private List<int> _triangles = new List<int>();
        private List<Vector2> _uvs = new List<Vector2>();
        private List<Vector3> _normals = new List<Vector3>();

        private bool _dirty;

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

            if (_mesh != null && _material != null) {
                Graphics.DrawMesh(_mesh, Vector3.zero, Quaternion.identity, _material, gameObject.layer);
            }
        }

        /// <summary>
        /// 値変化時処理
        /// </summary>
        private void OnValidate() {
            _dirty = true;
        }

        /// <summary>
        /// メッシュの生成
        /// </summary>
        private void GenerateMesh() {
            if (_mesh == null) {
                _mesh = new Mesh();
            }

            if (_width <= float.Epsilon || _unitDistance <= float.Epsilon || _path == null) {
                return;
            }
            
            _vertices.Clear();
            _triangles.Clear();
            _uvs.Clear();
            _normals.Clear();
            
            var totalDistance = _path.GetTotalDistance();
            
            // 頂点の生成
            var distance = 0.0f;
            var index = 0;
            while (distance <= totalDistance) {
                var point = _path.GetPointAtDistance(distance);
                var halfWidth = _width * 0.5f;
                var left = point.Position - point.Right * halfWidth;
                var right = point.Position + point.Right * halfWidth;
                _vertices.Add(left);
                _vertices.Add(right);
                _uvs.Add(new Vector2(0.0f, distance));
                _uvs.Add(new Vector2(1.0f, distance));
                _normals.Add(point.Normal);
                _normals.Add(point.Normal);
                if (index > 0) {
                    var idx = (index - 1) * 2;
                    _triangles.Add(idx + 0);
                    _triangles.Add(idx + 2);
                    _triangles.Add(idx + 1);
                    _triangles.Add(idx + 1);
                    _triangles.Add(idx + 2);
                    _triangles.Add(idx + 3);
                }
                distance += _unitDistance;
                index++;
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetNormals(_normals);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();
        }
    }
}