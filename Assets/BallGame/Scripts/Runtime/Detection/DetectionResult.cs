using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 4連結でまとめた変化画素の塊。
    /// </summary>
    public struct DepthBlob
    {
        /// <summary>面積（画素数）。</summary>
        public int Area;

        /// <summary>左端。</summary>
        public int MinX;

        /// <summary>上端。</summary>
        public int MinY;

        /// <summary>右端。</summary>
        public int MaxX;

        /// <summary>下端。</summary>
        public int MaxY;

        /// <summary>重心 x（画素）。</summary>
        public float CentroidX;

        /// <summary>重心 y（画素）。</summary>
        public float CentroidY;

        /// <summary>塊の中で一番強い点の強さ（壁からの距離÷しきい値）。</summary>
        public float PeakStrength;

        /// <summary>縦横の箱のうち塊が埋めている割合。</summary>
        public float FillRatio
        {
            get { return (float)Area / (Width * Height); }
        }

        /// <summary>横幅（画素）。</summary>
        public int Width
        {
            get { return MaxX - MinX + 1; }
        }

        /// <summary>縦幅（画素）。</summary>
        public int Height
        {
            get { return MaxY - MinY + 1; }
        }
    }

    /// <summary>
    /// 採用したヒット。
    /// </summary>
    public struct DetectedHit
    {
        /// <summary>深度画像上の重心（画素）。</summary>
        public Vector2 Pixel;

        /// <summary>深度画像上の 0〜1（上端が 0）。</summary>
        public Vector2 ImageUv;

        /// <summary>塊の面積。</summary>
        public int Area;
    }

    /// <summary>
    /// 1フレーム分の検知結果。
    /// </summary>
    public sealed class DetectionResult
    {
        /// <summary>このフレームで採用したヒット。</summary>
        public readonly List<DetectedHit> Hits = new List<DetectedHit>();

        /// <summary>このフレームで見つかった塊（孤立点を除いたあと）。</summary>
        public readonly List<DepthBlob> Blobs = new List<DepthBlob>();

        /// <summary>人とみなしたフレームか。</summary>
        public bool PersonDetected;

        /// <summary>人が去ったあとの待ち時間中か。</summary>
        public bool InCooldown;

        /// <summary>変化画素の合計（壁からの距離の上限に関係なく数える）。</summary>
        public int ChangedPixelCount;

        /// <summary>このフレームで一番大きい塊があるか。</summary>
        public bool HasLargestBlob;

        /// <summary>このフレームで一番大きい塊（調整用の表示に使う）。</summary>
        public DepthBlob LargestBlob;

        /// <summary>一番大きい塊を採用しない理由（採用できるときは空文字）。</summary>
        public string LargestBlobRejectReason = string.Empty;

        /// <summary>候補画素（プレビュー表示用。次のフレームで上書きされます）。</summary>
        public bool[] CandidateMask;

        /// <summary>
        /// 中身を空にします。
        /// </summary>
        public void Clear()
        {
            Hits.Clear();
            Blobs.Clear();
            PersonDetected = false;
            InCooldown = false;
            ChangedPixelCount = 0;
            HasLargestBlob = false;
            LargestBlobRejectReason = string.Empty;
        }

        /// <summary>
        /// 一番大きい塊の様子を、調整用の短い文にします。
        /// </summary>
        /// <returns>説明（塊が無いときは「塊なし」）</returns>
        public string DescribeLargestBlob()
        {
            if (!HasLargestBlob)
            {
                return "塊なし";
            }

            string verdict = LargestBlobRejectReason.Length == 0 ? "ボールとして採用できる" : "不採用：" + LargestBlobRejectReason;
            return string.Format("最大の塊 面積{0} 強さ{1:F2} 詰まり{2:F2}（{3}）", LargestBlob.Area, LargestBlob.PeakStrength, LargestBlob.FillRatio, verdict);
        }
    }
}
