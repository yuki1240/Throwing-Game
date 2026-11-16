namespace BallGame
{
    /// <summary>
    /// ボール検知のしきい値。
    /// </summary>
    public sealed class DetectionSettings
    {
        /// <summary>ノイズ床（mm）。これ未満はセンサーの揺れとして捨てる。</summary>
        public int NoiseFloorMm = CalibrationData.DefaultNoiseFloorMm;

        /// <summary>壁からの距離の上限（mm）。これより遠いものは捨てる。</summary>
        public int MaxDistanceMm = CalibrationData.DefaultMaxDistanceMm;

        /// <summary>これ未満の面積の塊は捨てる。</summary>
        public int MinArea = 16;

        /// <summary>これ以上の面積の塊は1フレームで採用する。</summary>
        public int InstantArea = 32;

        /// <summary>小さい塊を採用するまでに続けて見えるフレーム数。</summary>
        public int ConfirmFrames = 2;

        /// <summary>変化画素の合計がこれを超えたら人とみなす。</summary>
        public int PersonPixelTotal = 4500;

        /// <summary>塊の縦横どちらかがこれを超えたら人とみなす（画素）。</summary>
        public int PersonMaxExtent = 80;

        /// <summary>人が去ったあと待つ秒数。</summary>
        public float PersonCooldownSeconds = 0.5f;

        /// <summary>同じ物体とみなす重心の距離（画素）。</summary>
        public float TrackMatchRadius = 8f;

        /// <summary>見えなくなった物体を覚えておくフレーム数（同じ場所に留まった物体を出し続けないため）。</summary>
        public int TrackForgetFrames = 10;

        /// <summary>
        /// 校正データのしきい値を取り込みます。
        /// </summary>
        /// <param name="data">校正データ</param>
        public void ApplyCalibration(CalibrationData data)
        {
            NoiseFloorMm = data.NoiseFloorMm;
            MaxDistanceMm = data.MaxDistanceMm;
        }
    }
}
