using System;
using System.Linq;
using UnityEngine;

namespace CourseGenerator {
    /// <summary>
    /// コース用のパス
    /// </summary>
    [ExecuteAlways]
    public class Path : MonoBehaviour {
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
        private class PathNode {
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
        private PathNode[] _pathNodes;

        public GameObject TestObj;
        [Range(0.0f, 1.0f)]
        public float Test;

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
        /// 現在のPointを取得
        /// </summary>
        public Point GetPoint(float rate) {
            var totalDistance = _pathNodes.Sum(x => GetDistance(x));
            var targetDistance = totalDistance * rate;
            return GetPointAtDistance(targetDistance);
        }

        /// <summary>
        /// 現在のPointを取得
        /// </summary>
        public Point GetPointAtDistance(float targetDistance) {
            var startPoint = GetStartPoint();
            for (var i = 0; i < _pathNodes.Length; i++) {
                var distance = GetDistance(_pathNodes[i]);
                if (distance < targetDistance) {
                    targetDistance -= distance;
                    startPoint = GetEndPoint(startPoint, _pathNodes[i]);
                    continue;
                }
                
                return GetPointAtDistance(startPoint, _pathNodes[i], targetDistance);
            }

            return startPoint;
        }

        /// <summary>
        /// トータルの長さを取得
        /// </summary>
        private float GetDistance(PathNode node) {
            switch (node.pathType) {
                case PathType.Straight:
                    return node.straightDistance;
                case PathType.Corner:
                    return node.curveRadius * Mathf.Abs(node.curveAngle) * Mathf.Deg2Rad;
            }

            return node.straightDistance;
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
        /// ポイントの取得
        /// </summary>
        private Point GetPoint(Point startPoint, PathNode node, float rate) {
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
        private Point GetPointAtDistance(Point startPoint, PathNode node, float distance) {
            var totalDistance = GetDistance(node);
            return GetPoint(startPoint, node, Mathf.Clamp01(distance / totalDistance));
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
            currentEulerAngles.z = bank > 0.0f ? Mathf.Max(currentEulerAngles.z, bank) : Mathf.Min(currentEulerAngles.z, bank);
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
        /// PathNode毎のGizmo描画
        /// </summary>
        private Point DrawPathNodeGizmos(Point startPoint, PathNode node) {
            // ライン描画
            var splitDistance = 1.0f;
            var lineCount = (int)(GetDistance(node) / splitDistance) + 1;
            var totalDistance = 0.0f;
            for (var i = 0; i < lineCount; i++) {
                var distance = totalDistance;
                totalDistance += splitDistance;
                var nextDistance = totalDistance;
                var point = GetPointAtDistance(startPoint, node, distance);
                var nextPoint = GetPointAtDistance(startPoint, node, nextDistance);
                
                // パス
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(point.Position, nextPoint.Position);
                
                if (i > 0) {
                    // 法線
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(point.Position, point.Position + point.Normal * 0.5f);
                }
            }

            var endPoint = GetPoint(startPoint, node, 1.0f);
            
            // 終点描画
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(endPoint.Position, 0.15f);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(endPoint.Position, endPoint.Position + endPoint.Normal * 0.5f);
            
            return endPoint;
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
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(startPoint.Position, 0.15f);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(startPoint.Position, startPoint.Position + startPoint.Normal * 0.5f);
            
            // PathNodeの描画
            for (var i = 0; i < _pathNodes.Length; i++) {
                startPoint = DrawPathNodeGizmos(startPoint, _pathNodes[i]);
            }

            Gizmos.color = prevColor;
        }

        private void Update() {
            if (TestObj != null) {
                var point = GetPoint(Test);
                var trans = TestObj.transform;
                trans.position = point.Position;
                trans.rotation = point.Rotation;
            }
        }
    }
}