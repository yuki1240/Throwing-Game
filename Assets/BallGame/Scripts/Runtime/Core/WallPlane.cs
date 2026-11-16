using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 深度カメラの内部パラメータ。画素と深度（mm）から3次元の点（mm）を作ります。
    /// 座標は x 右、y 下、z 奥（カメラ座標）です。
    /// </summary>
    public readonly struct DepthIntrinsics
    {
        /// <summary>Kinect v2 の深度（512 画素幅）での焦点距離。</summary>
        public const float KinectFocalLength512 = 365.5f;

        /// <summary>横の焦点距離（画素）。</summary>
        public readonly float FocalX;

        /// <summary>縦の焦点距離（画素）。</summary>
        public readonly float FocalY;

        /// <summary>光軸の横位置（画素）。</summary>
        public readonly float CenterX;

        /// <summary>光軸の縦位置（画素）。</summary>
        public readonly float CenterY;

        /// <summary>
        /// 値を指定して作ります。
        /// </summary>
        /// <param name="focalX">横の焦点距離</param>
        /// <param name="focalY">縦の焦点距離</param>
        /// <param name="centerX">光軸の横位置</param>
        /// <param name="centerY">光軸の縦位置</param>
        public DepthIntrinsics(float focalX, float focalY, float centerX, float centerY)
        {
            FocalX = focalX;
            FocalY = focalY;
            CenterX = centerX;
            CenterY = centerY;
        }

        /// <summary>
        /// 解像度から Kinect v2 相当の内部パラメータを作ります（模擬深度にも同じ比率を使います）。
        /// </summary>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <returns>内部パラメータ</returns>
        public static DepthIntrinsics ForResolution(int width, int height)
        {
            float focal = KinectFocalLength512 * width / 512f;
            return new DepthIntrinsics(focal, focal, width * 0.5f, height * 0.5f);
        }

        /// <summary>
        /// 画素の視線方向（z=1 に正規化）を返します。
        /// </summary>
        /// <param name="pixelX">画素の x</param>
        /// <param name="pixelY">画素の y（上端が 0）</param>
        /// <returns>視線方向</returns>
        public Vector3 GetRay(float pixelX, float pixelY)
        {
            return new Vector3((pixelX - CenterX) / FocalX, (pixelY - CenterY) / FocalY, 1f);
        }

        /// <summary>
        /// 画素と深度から3次元の点を返します。
        /// </summary>
        /// <param name="pixelX">画素の x</param>
        /// <param name="pixelY">画素の y</param>
        /// <param name="depthMm">深度（mm）</param>
        /// <returns>カメラ座標の点（mm）</returns>
        public Vector3 Unproject(float pixelX, float pixelY, float depthMm)
        {
            return GetRay(pixelX, pixelY) * depthMm;
        }
    }

    /// <summary>
    /// 壁の面。四隅の基準深度から作り、点から面までの垂直距離を求めます。
    /// 距離はカメラ側を正にします（壁より手前にある物ほど大きい）。
    /// </summary>
    public readonly struct WallPlane
    {
        /// <summary>単位法線（カメラ側を向く）。</summary>
        public readonly Vector3 Normal;

        /// <summary>面の式 n・p + Offset = 0 の定数項。</summary>
        public readonly float Offset;

        /// <summary>面として使えるか。</summary>
        public readonly bool IsValid;

        private WallPlane(Vector3 normal, float offset, bool isValid)
        {
            Normal = normal;
            Offset = offset;
            IsValid = isValid;
        }

        /// <summary>
        /// 4点（左上・右上・右下・左下の順）から面を作ります。対角線どうしの外積で法線を求めます。
        /// </summary>
        /// <param name="points">壁の上の4点（mm）</param>
        /// <returns>壁の面</returns>
        public static WallPlane FromCornerPoints(IList<Vector3> points)
        {
            if (points == null || points.Count != 4)
            {
                return default;
            }

            Vector3 normal = Vector3.Cross(points[2] - points[0], points[3] - points[1]);
            if (normal.sqrMagnitude < 1e-6f)
            {
                return default;
            }

            normal.Normalize();
            Vector3 centroid = (points[0] + points[1] + points[2] + points[3]) * 0.25f;
            return OrientTowardCamera(normal, -Vector3.Dot(normal, centroid));
        }

        /// <summary>
        /// 法線と定数項から面を作ります。
        /// </summary>
        /// <param name="normal">法線（正規化される）</param>
        /// <param name="offset">定数項</param>
        /// <returns>壁の面</returns>
        public static WallPlane FromNormalAndOffset(Vector3 normal, float offset)
        {
            float length = normal.magnitude;
            if (length < 1e-6f)
            {
                return default;
            }

            return OrientTowardCamera(normal / length, offset / length);
        }

        /// <summary>
        /// 点から面までの垂直距離を返します（カメラ側が正）。
        /// </summary>
        /// <param name="point">点（mm）</param>
        /// <returns>距離（mm）</returns>
        public float DistanceTo(Vector3 point)
        {
            return Vector3.Dot(Normal, point) + Offset;
        }

        private static WallPlane OrientTowardCamera(Vector3 normal, float offset)
        {
            // カメラ（原点）での値が正になるように向きをそろえる
            if (offset < 0f)
            {
                return new WallPlane(-normal, -offset, true);
            }

            return new WallPlane(normal, offset, true);
        }
    }

    /// <summary>
    /// 基準深度と四隅から壁の面を作るクラス。
    /// </summary>
    public static class WallPlaneBuilder
    {
        /// <summary>四隅の近くで有効な基準を探す半径（画素）。</summary>
        public const int SearchRadius = 12;

        /// <summary>
        /// 基準深度と四隅から壁の面を作ります。
        /// </summary>
        /// <param name="baseline">基準深度</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <param name="corners">四隅（深度画像上の 0〜1）</param>
        /// <param name="intrinsics">内部パラメータ</param>
        /// <returns>壁の面（作れないときは IsValid が false）</returns>
        public static WallPlane Build(ushort[] baseline, int width, int height, IList<Vector2> corners, DepthIntrinsics intrinsics)
        {
            if (baseline == null || corners == null || corners.Count != 4)
            {
                return default;
            }

            Vector3[] points = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                if (!TryGetCornerPoint(baseline, width, height, corners[i], intrinsics, out points[i]))
                {
                    return default;
                }
            }

            return WallPlane.FromCornerPoints(points);
        }

        private static bool TryGetCornerPoint(ushort[] baseline, int width, int height, Vector2 cornerUv, DepthIntrinsics intrinsics, out Vector3 point)
        {
            int centerX = Mathf.Clamp(Mathf.FloorToInt(cornerUv.x * width), 0, width - 1);
            int centerY = Mathf.Clamp(Mathf.FloorToInt(cornerUv.y * height), 0, height - 1);
            for (int radius = 0; radius <= SearchRadius; radius++)
            {
                if (TryMedianAround(baseline, width, height, centerX, centerY, radius, intrinsics, out point))
                {
                    return true;
                }
            }

            point = Vector3.zero;
            return false;
        }

        private static bool TryMedianAround(ushort[] baseline, int width, int height, int centerX, int centerY, int radius, DepthIntrinsics intrinsics, out Vector3 point)
        {
            List<Vector3> samples = new List<Vector3>();
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    AddSample(baseline, width, height, x, y, intrinsics, samples);
                }
            }

            if (samples.Count == 0)
            {
                point = Vector3.zero;
                return false;
            }

            samples.Sort((a, b) => a.z.CompareTo(b.z));
            point = samples[samples.Count / 2];
            return true;
        }

        private static void AddSample(ushort[] baseline, int width, int height, int x, int y, DepthIntrinsics intrinsics, List<Vector3> samples)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            ushort depth = baseline[y * width + x];
            if (depth == 0)
            {
                return;
            }

            samples.Add(intrinsics.Unproject(x + 0.5f, y + 0.5f, depth));
        }
    }
}
