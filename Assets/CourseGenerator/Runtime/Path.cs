using System;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace CourseGenerator {
    /// <summary>
    /// コース用のパス
    /// </summary>
    [ExecuteAlways]
    public class Path : MonoBehaviour {
        // コーナーの角度
        private const float CornerAngle = 90.0f;
        private const float CornerRadian = CornerAngle * Mathf.Deg2Rad;
        
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
            public Quaternion Rotation;
            public Vector3 Normal;
            public Vector3 Forward;
            public Vector3 Right;
        }

        /// <summary>
        /// カーブタイプ
        /// </summary>
        private enum CurveType {
            Left,
            Right,
        }

        /// <summary>
        /// スロープタイプ
        /// </summary>
        private enum SlopeType {
            ToFlat,
            ToSharp,
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
            [SerializeField, Tooltip("カーブ角度"), Range(-90.0f, 90.0f)]
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
                Rotation = trans.rotation,
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
                    startPoint = GetPoint(startPoint, _pathNodes[i], 1.0f);
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
                    return GetStraightPoint(startPoint, node, rate);
                case PathType.Corner:
                    return GetCornerPoint(startPoint, node, rate);
            }

            return GetStraightPoint(startPoint, node, rate);
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
            var flatRight = startPoint.Right;
            flatRight.y = 0.0f;
            flatRight.Normalize();
            var forward = Quaternion.Euler(node.slope, 0.0f, 0.0f) * flatForward;
            var right = Vector3.Slerp(startPoint.Right, Quaternion.Euler(0.0f, 0.0f, node.tilt) * flatRight, rate);
            var normal = Vector3.Cross(forward, right);
            var position = startPoint.Position + node.straightDistance * rate * forward;
            return new Point {
                Position = position,
                Rotation = Quaternion.LookRotation(forward, normal),
                Normal = normal,
                Forward = forward,
                Right = right,
            };
        }

        /// <summary>
        /// ストレート終端ポイントの取得
        /// </summary>
        private Point GetStraightEndPoint(Point startPoint, PathNode node) {
            var flatForward = startPoint.Forward;
            flatForward.y = 0.0f;
            flatForward.Normalize();
            var flatRight = startPoint.Right;
            flatRight.y = 0.0f;
            flatRight.Normalize();
            var startEulerAngles = startPoint.Rotation.eulerAngles;
            var endEulerAngles = new Vector3(node.slope, startEulerAngles.y, node.tilt);
            var currentRotate = Quaternion.Euler(endEulerAngles);
            var forward = currentRotate * Vector3.forward;
            var right = currentRotate * Vector3.right;
            var normal = currentRotate * Vector3.up;
            var position = startPoint.Position + node.straightDistance * startPoint.Forward;
            var v0 = Mathf.Tan(startEulerAngles.x * Mathf.Deg2Rad);
            var v1 = Mathf.Tan(endEulerAngles.x * Mathf.Deg2Rad);
            //var down = (v0 * rate + 0.5f * (v1 - v0) * rate * rate) * distance * Vector3.down;
            return new Point {
                Position = position,
                Rotation = Quaternion.LookRotation(forward, normal),
                Normal = normal,
                Forward = forward,
                Right = right,
            };
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
            var startEulerAngles = startPoint.Rotation.eulerAngles;
            var endEulerAngles = new Vector3(node.slope, startEulerAngles.y + curveAngle, node.tilt);
            var currentEulerAngles = LerpAngles(startEulerAngles, endEulerAngles, rate);
            currentEulerAngles.z -= Mathf.Sin(rate * Mathf.PI) * node.bank * curveAngle / 180.0f;
            var currentRotate = Quaternion.Euler(currentEulerAngles);
            var forward = currentRotate * Vector3.forward;
            var right = currentRotate * Vector3.right;
            var normal = currentRotate * Vector3.up;
            
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
            var position = startPoint.Position + Quaternion.LookRotation(flatForward) * (curvePivot + vector) + down;
            return new Point {
                Position = position,
                Rotation = currentRotate,
                Normal = normal,
                Forward = forward,
                Right = right,
            };
        }

        /// <summary>
        /// 角度の線形補間
        /// </summary>
        private Vector3 LerpAngles(Vector3 a, Vector3 b, float t) {
            return new Vector3(
                Mathf.LerpAngle(a.x, b.x, t),
                Mathf.LerpAngle(a.y, b.y, t),
                Mathf.LerpAngle(a.z, b.z, t)
            );
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
                trans.rotation = quaternion.LookRotation(point.Forward, point.Normal);
            }
        }
    }
}