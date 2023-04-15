using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CourseGenerator {
    /// <summary>
    /// コース用のパス
    /// </summary>
    [ExecuteAlways]
    public class CoursePath : MonoBehaviour {
        /// <summary>
        /// パスのタイプ
        /// </summary>
        public enum PathType {
            // 直線
            Straight,
            // コーナー
            Corner,
        }

        /// <summary>
        /// ポイント情報
        /// </summary>
        public struct Point {
            public Vector3 Position;
            public Vector3 EulerAngles;
            public Vector3 Normal;
            public Vector3 Forward;
            public Vector3 Right;

            public Quaternion Rotation => Quaternion.Euler(EulerAngles);
        }

        /// <summary>
        /// パスを構成するノード情報
        /// </summary>
        [Serializable]
        public class PathNode {
            [Tooltip("パスのタイプ")] 
            public PathType pathType = PathType.Corner;

            [Header("Common")]
            [SerializeField, Tooltip("勾配")]
            public float slope;
            [SerializeField, Tooltip("傾き")]
            public float tilt;

            [Header("Straight")]
            [SerializeField, Tooltip("直線距離")]
            public float straightDistance;

            [Header("Corner")]
            [SerializeField, Tooltip("カーブ半径")]
            public float curveRadius;
            [SerializeField, Tooltip("カーブ角度"), Range(-180.0f, 180.0f)]
            public float curveAngle;
            [SerializeField, Tooltip("バンク"), Range(0.0f, 90.0f)]
            public float bank;
        }

        [SerializeField, Tooltip("ノードリスト")]
        private List<PathNode> _pathNodes;
        [SerializeField, Tooltip("プレビュー用オブジェクト")]
        private GameObject _previewObject;
        [SerializeField, Tooltip("プレビュー用進捗スライダー"), Range(0.0f, 1.0f)]
        private float _previewProgress;

        // 計算済みのPointリスト
        private readonly List<Point> _cachedPoints = new List<Point>();
        // 計算済みのトータル距離
        private float _cachedTotalDistance;
        // キャッシュが無効か
        private bool _dirtyCache;

        // PathNodeリスト
        public IReadOnlyList<PathNode> PathNodes => _pathNodes;

        // Path更新イベント
        public event Action OnUpdatedPathEvent;

        /// <summary>
        /// Pathのクリア
        /// </summary>
        public void ClearPaths() {
            _pathNodes.Clear();
            
            OnUpdatedPath();
        }

        /// <summary>
        /// 直線Pathの追加
        /// </summary>
        /// <param name="slope">勾配</param>
        /// <param name="tilt">傾き</param>
        /// <param name="distance">距離(XZ)</param>
        public PathNode AddStraightPath(float slope, float tilt, float distance) {
            distance = Mathf.Max(distance, 0.0f);
            
            var pathNode = new PathNode();
            pathNode.pathType = PathType.Straight;
            pathNode.slope = slope;
            pathNode.tilt = tilt;
            pathNode.straightDistance = distance;
            _pathNodes.Add(pathNode);
            
            OnUpdatedPath();
            return pathNode;
        }

        /// <summary>
        /// コーナーPathの追加
        /// </summary>
        /// <param name="slope">勾配</param>
        /// <param name="tilt">傾き</param>
        /// <param name="curveRadius">カーブ半径</param>
        /// <param name="curveAngle">カーブ角度(-180～180)</param>
        /// <param name="bank">バンク(0～90)</param>
        public PathNode AddCornerPath(float slope, float tilt, float curveRadius, float curveAngle, float bank) {
            curveRadius = Mathf.Max(curveRadius, 0.0f);
            curveAngle = Mathf.Clamp(curveAngle, -180, 180);
            bank = Mathf.Clamp(bank, 0, 90);
            
            var pathNode = new PathNode();
            pathNode.pathType = PathType.Corner;
            pathNode.slope = slope;
            pathNode.tilt = tilt;
            pathNode.curveRadius = curveRadius;
            pathNode.curveAngle = curveAngle;
            pathNode.bank = bank;
            _pathNodes.Add(pathNode);
            
            OnUpdatedPath();
            return pathNode;
        }

        /// <summary>
        /// 開始位置の取得
        /// </summary>
        public Point GetStartPoint() {
            var trans = transform;
            return new Point {
                Position = trans.position,
                EulerAngles = trans.eulerAngles,
                Forward = trans.forward,
                Right = trans.right,
                Normal = trans.up
            };
        }

        /// <summary>
        /// 終端位置の取得
        /// </summary>
        public Point GetEndPoint() {
            RefreshCache();
            return _cachedPoints[_cachedPoints.Count - 1];
        }

        /// <summary>
        /// 特定IndexのPointを取得
        /// </summary>
        public Point GetPoint(int index) {
            RefreshCache();
            
            index = Mathf.Clamp(index, 0, _cachedPoints.Count - 1);
            
            if (index < 0) {
                return GetStartPoint();
            }

            return _cachedPoints[index];
        }

        /// <summary>
        /// 現在のPointを取得
        /// </summary>
        public Point GetPoint(float rate) {
            var totalDistance = GetTotalDistance();
            var targetDistance = totalDistance * rate;
            return GetPointAtDistance(targetDistance);
        }

        /// <summary>
        /// 現在のPointを取得
        /// </summary>
        public Point GetPointAtDistance(float targetDistance) {
            RefreshCache();
            
            var startPoint = GetStartPoint();
            for (var i = 0; i < _pathNodes.Count; i++) {
                var distance = GetDistance(_pathNodes[i]);
                if (distance < targetDistance) {
                    targetDistance -= distance;
                    startPoint = GetPoint(i + 1);
                    continue;
                }
                
                return GetPointAtDistance(startPoint, _pathNodes[i], targetDistance);
            }

            return startPoint;
        }

        /// <summary>
        /// トータル距離の取得
        /// </summary>
        public float GetTotalDistance() {
            RefreshCache();
            return _cachedTotalDistance;
        }

        /// <summary>
        /// トータルの長さを取得
        /// </summary>
        public float GetDistance(PathNode node) {
            if (!IsValidPathNode(node)) {
                return 0.0f;
            }
            
            switch (node.pathType) {
                case PathType.Straight:
                    return node.straightDistance;
                case PathType.Corner:
                    return node.curveRadius * Mathf.Abs(node.curveAngle) * Mathf.Deg2Rad;
            }

            return node.straightDistance;
        }

        /// <summary>
        /// ポイントの取得
        /// </summary>
        public Point GetPoint(Point startPoint, PathNode node, float rate) {
            if (!IsValidPathNode(node)) {
                return startPoint;
            }
            
            rate = Mathf.Clamp01(rate);

            switch (node.pathType) {
                case PathType.Straight:
                    return GetStraightPoint(startPoint, node, rate);
                case PathType.Corner:
                    return GetCornerPoint(startPoint, node, rate);
            }

            return GetStraightPoint(startPoint, node, rate);
        }

        /// <summary>
        /// 距離ベースのポイントの取得
        /// </summary>
        public Point GetPointAtDistance(Point startPoint, PathNode node, float distance) {
            var totalDistance = GetDistance(node);
            return GetPoint(startPoint, node, Mathf.Clamp01(distance / totalDistance));
        }

        /// <summary>
        /// 終端の取得
        /// </summary>
        private Point GetEndPoint(Point startPoint, PathNode node) {
            switch (node.pathType) {
                case PathType.Straight:
                    return GetStraightEndPoint(startPoint, node);
                case PathType.Corner:
                    return GetCornerEndPoint(startPoint, node);
            }

            return GetStraightEndPoint(startPoint, node);
        }

        /// <summary>
        /// ストレートポイントの取得
        /// </summary>
        private Point GetStraightPoint(Point startPoint, PathNode node, float rate) {
            var flatForward = startPoint.Forward;
            flatForward.y = 0.0f;
            flatForward.Normalize();
            var startEulerAngles = startPoint.EulerAngles;
            var endEulerAngles = new Vector3(node.slope, startEulerAngles.y, node.tilt);
            var currentEulerAngles = Vector3.Lerp(startEulerAngles, endEulerAngles, rate);
            var currentRotation = Quaternion.Euler(currentEulerAngles);
            var forward = currentRotation * Vector3.forward;
            var right = currentRotation * Vector3.right;
            var normal = currentRotation * Vector3.up;
            var distance = node.straightDistance;
            var v0 = Mathf.Tan(startEulerAngles.x * Mathf.Deg2Rad);
            var v1 = Mathf.Tan(endEulerAngles.x * Mathf.Deg2Rad);
            var down = (v0 * rate + 0.5f * (v1 - v0) * rate * rate) * distance * Vector3.down;
            var position = startPoint.Position + distance * rate * flatForward + down;
            return new Point {
                Position = position,
                EulerAngles = currentEulerAngles,
                Normal = normal,
                Forward = forward,
                Right = right,
            };
        }

        /// <summary>
        /// ストレート終端ポイントの取得
        /// </summary>
        private Point GetStraightEndPoint(Point startPoint, PathNode node) {
            return GetStraightPoint(startPoint, node, 1.0f);
        }

        /// <summary>
        /// コーナーポイントの取得
        /// </summary>
        private Point GetCornerPoint(Point startPoint, PathNode node, float rate) {
            var flatForward = startPoint.Forward;
            flatForward.y = 0.0f;
            flatForward.Normalize();
            var flatRight = startPoint.Right;
            flatRight.y = 0.0f;
            flatRight.Normalize();
            var curveAngle = node.curveAngle;
            var startEulerAngles = startPoint.EulerAngles;
            var endEulerAngles = new Vector3(node.slope, startEulerAngles.y + curveAngle, node.tilt);
            var currentEulerAngles = Vector3.Lerp(startEulerAngles, endEulerAngles, rate);
            var bank = -Mathf.Sin(rate * Mathf.PI) * node.bank * node.curveAngle / 180.0f;
            currentEulerAngles.z += bank;
            var currentRotation = Quaternion.Euler(currentEulerAngles);
            var forward = currentRotation * Vector3.forward;
            var right = currentRotation * Vector3.right;
            var normal = currentRotation * Vector3.up;
            
            var curvePivot = (curveAngle > 0.0f ? Vector3.right : Vector3.left) * node.curveRadius;
            var pivotVector = -curvePivot;
            var theta = curveAngle * Mathf.Deg2Rad * rate;
            var cosTheta = Mathf.Cos(theta);
            var sinTheta = Mathf.Sin(theta);
            var vector = new Vector3(
                pivotVector.x * cosTheta + pivotVector.z * sinTheta,
                0.0f,
                -pivotVector.x * sinTheta + pivotVector.z * cosTheta);
            var distance = GetDistance(node);
            var v0 = Mathf.Tan(startEulerAngles.x * Mathf.Deg2Rad);
            var v1 = Mathf.Tan(endEulerAngles.x * Mathf.Deg2Rad);
            var down = (v0 * rate + 0.5f * (v1 - v0) * rate * rate) * distance * Vector3.down;
            var position = startPoint.Position + Quaternion.Euler(0.0f, startEulerAngles.y, 0.0f) * (curvePivot + vector) + down;
            return new Point {
                Position = position,
                EulerAngles = currentEulerAngles,
                Normal = normal,
                Forward = forward,
                Right = right,
            };
        }

        /// <summary>
        /// コーナー終端ポイントの取得
        /// </summary>
        private Point GetCornerEndPoint(Point startPoint, PathNode node) {
            return GetCornerPoint(startPoint, node, 1.0f);
        }

        /// <summary>
        /// 有効なNodeかチェック
        /// </summary>
        private bool IsValidPathNode(PathNode node) {
            switch (node.pathType) {
                case PathType.Straight: {
                    if (node.straightDistance <= float.Epsilon || node.slope >= 90.0f - float.Epsilon ||
                        node.slope <= -90 + float.Epsilon) {
                        return false;
                    }

                    break;
                }
                case PathType.Corner: {
                    if (node.curveRadius <= float.Epsilon || Mathf.Abs(node.curveAngle) <= float.Epsilon) {
                        return false;
                    }

                    break;
                }
            }

            return true;
        }

        /// <summary>
        /// 計算結果のキャッシュ
        /// </summary>
        private void RefreshCache(bool force = false) {
            if (!force && !_dirtyCache) {
                return;
            }
            
            _cachedPoints.Clear();
            _cachedTotalDistance = 0.0f;
            
            var startPoint = GetStartPoint();
            _cachedPoints.Add(startPoint);
            for (var i = 0; i < _pathNodes.Count; i++) {
                startPoint = GetPoint(startPoint, _pathNodes[i], 1.0f);
                _cachedTotalDistance += GetDistance(_pathNodes[i]);
                _cachedPoints.Add(startPoint);
            }

            _dirtyCache = false;
        }

        /// <summary>
        /// Path更新時処理
        /// </summary>
        private void OnUpdatedPath() {
            _dirtyCache = true;
            OnUpdatedPathEvent?.Invoke();
        }

        /// <summary>
        /// PathNode毎のGizmo描画
        /// </summary>
        private Point DrawPathNodeGizmos(Point startPoint, PathNode node) {var endPoint = GetPoint(startPoint, node, 1.0f);
            // 終点描画
            Gizmos.color = new Color(0.5f, 0.5f, 1.0f, 0.75f);
            Gizmos.DrawSphere(endPoint.Position, 0.5f);
            
            return endPoint;
        }
        
        /// <summary>
        /// 生成時処理
        /// </summary>
        private void Awake() {
            OnUpdatedPath();
            RefreshCache();
        }

        /// <summary>
        /// 値変化時
        /// </summary>
        private void OnValidate() {
            OnUpdatedPath();
        }

        /// <summary>
        /// ギズモ描画
        /// </summary>
        private void OnDrawGizmos() {
            var prevColor = Gizmos.color;
            
            // 始点の初期化
            var trans = transform;
            var startPoint = new Point {
                Position = trans.position,
                Forward = trans.forward,
                Right = trans.right,
                Normal = trans.up
            };

            // 始点描画
            Gizmos.color = new Color(1.0f, 0.5f, 0.5f, 0.75f);
            Gizmos.DrawSphere(startPoint.Position, 0.5f);
            
            // PathNodeの描画
            for (var i = 0; i < _pathNodes.Count; i++) {
                startPoint = DrawPathNodeGizmos(startPoint, _pathNodes[i]);
            }

            Gizmos.color = prevColor;
        }

        /// <summary>
        /// 更新処理
        /// </summary>
        private void Update() {
            if (_dirtyCache) {
                RefreshCache();
            }
            
            if (!Application.isPlaying) {
                if (_previewObject != null) {
                    var point = GetPoint(_previewProgress);
                    var trans = _previewObject.transform;
                    trans.position = point.Position;
                    trans.rotation = point.Rotation;
                }
            }
        }
    }
}