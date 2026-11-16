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
        }
    }
}
