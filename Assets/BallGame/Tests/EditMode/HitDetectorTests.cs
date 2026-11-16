using NUnit.Framework;

namespace BallGame.Tests
{
    /// <summary>
    /// ボール検知と人サイズの除外のテスト。
    /// </summary>
    public sealed class HitDetectorTests
    {
        private TestDepthScene scene;
        private HitDetector detector;

        [SetUp]
        public void SetUp()
        {
            scene = new TestDepthScene();
            detector = scene.CreateDetector();
        }

        [Test]
        public void 何も無ければヒットしない()
        {
            DetectionResult result = detector.Process(scene.EmptyFrame(), 0f);

            Assert.That(result.Hits, Is.Empty);
            Assert.That(result.ChangedPixelCount, Is.EqualTo(0));
        }

        [Test]
        public void 大きめのボールは1フレームで重心にヒットする()
        {
            ushort[] frame = scene.EmptyFrame();
            Assert.That(scene.DrawDisc(frame, 100f, 80f, 4.5f, 70f), Is.GreaterThanOrEqualTo(32));

            DetectionResult result = detector.Process(frame, 0f);

            Assert.That(result.Hits.Count, Is.EqualTo(1));
            Assert.That(result.Hits[0].Pixel.x, Is.EqualTo(99.5f).Within(0.6f));
            Assert.That(result.Hits[0].Pixel.y, Is.EqualTo(79.5f).Within(0.6f));
            Assert.That(result.Hits[0].ImageUv.x, Is.EqualTo(100f / scene.Width).Within(0.01f));
        }

        [Test]
        public void 小さい塊は2フレーム続いてから採用する()
        {
            ushort[] frame = scene.EmptyFrame();
            int area = scene.DrawDisc(frame, 200f, 120f, 2.8f, 70f);
            Assert.That(area, Is.InRange(16, 31));

            Assert.That(detector.Process(frame, 0f).Hits, Is.Empty);
            Assert.That(detector.Process(frame, 0.033f).Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void 面積16未満は捨てる()
        {
            ushort[] frame = scene.EmptyFrame();
            Assert.That(scene.DrawDisc(frame, 200f, 120f, 1.9f, 70f), Is.LessThan(16));

            for (int i = 0; i < 4; i++)
            {
                Assert.That(detector.Process(frame, i * 0.033f).Hits, Is.Empty);
            }
        }

        [Test]
        public void 孤立した点は捨てる()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawRect(frame, 150, 100, 1, 1, 70f);

            DetectionResult result = detector.Process(frame, 0f);

            Assert.That(result.ChangedPixelCount, Is.EqualTo(1));
            Assert.That(result.Blobs, Is.Empty);
        }

        [Test]
        public void ノイズ床未満と上限より遠いものは捨てる()
        {
            ushort[] near = scene.EmptyFrame();
            scene.DrawDisc(near, 100f, 100f, 5f, 30f);
            Assert.That(detector.Process(near, 0f).Hits, Is.Empty);

            ushort[] far = scene.EmptyFrame();
            scene.DrawDisc(far, 220f, 100f, 5f, 300f);
            DetectionResult result = detector.Process(far, 1f);
            Assert.That(result.Hits, Is.Empty);
            Assert.That(result.ChangedPixelCount, Is.GreaterThan(0));
        }

        [Test]
        public void 壁からの距離の上限を広げると遠いものも拾う()
        {
            detector.Settings.MaxDistanceMm = 400;
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 220f, 100f, 5f, 300f);

            Assert.That(detector.Process(frame, 0f).Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void 深度0になった物体も候補にする()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 160f, 120f, 4.5f, -1f);

            Assert.That(detector.Process(frame, 0f).Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void 四隅の外側は見ない()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 6f, 6f, 4.5f, 70f);

            Assert.That(detector.Process(frame, 0f).Hits, Is.Empty);
        }

        [Test]
        public void 縦横が80画素を超える塊は人として除外する()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawRect(frame, 60, 40, 30, 90, 80f);
            scene.DrawDisc(frame, 250f, 60f, 4.5f, 70f);

            DetectionResult result = detector.Process(frame, 0f);

            Assert.That(result.PersonDetected, Is.True);
            Assert.That(result.Hits, Is.Empty);
        }

        [Test]
        public void 変化画素が4500を超えると壁から遠くても人として除外する()
        {
            ushort[] frame = scene.EmptyFrame();
            Assert.That(scene.DrawRect(frame, 60, 40, 70, 70, 400f), Is.GreaterThan(4500));
            scene.DrawDisc(frame, 250f, 60f, 4.5f, 70f);

            DetectionResult result = detector.Process(frame, 0f);

            Assert.That(result.PersonDetected, Is.True);
            Assert.That(result.Hits, Is.Empty);
        }

        [Test]
        public void 人が去ってから0点5秒はボールを出さない()
        {
            ushort[] person = scene.EmptyFrame();
            scene.DrawRect(person, 60, 40, 30, 90, 80f);
            detector.Process(person, 0f);

            ushort[] ball = scene.EmptyFrame();
            scene.DrawDisc(ball, 250f, 60f, 4.5f, 70f);
            DetectionResult during = detector.Process(ball, 0.3f);
            Assert.That(during.InCooldown, Is.True);
            Assert.That(during.Hits, Is.Empty);

            ushort[] later = scene.EmptyFrame();
            scene.DrawDisc(later, 150f, 180f, 4.5f, 70f);
            Assert.That(detector.Process(later, 0.6f).Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void 同じ場所に留まった物体は出し続けない()
        {
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 100f, 100f, 4.5f, 70f);
            int total = 0;
            for (int i = 0; i < 30; i++)
            {
                total += detector.Process(frame, i * 0.033f).Hits.Count;
            }

            Assert.That(total, Is.EqualTo(1));
        }

        [Test]
        public void 別の場所に当たればまたヒットする()
        {
            ushort[] first = scene.EmptyFrame();
            scene.DrawDisc(first, 100f, 100f, 4.5f, 70f);
            ushort[] second = scene.EmptyFrame();
            scene.DrawDisc(second, 220f, 150f, 4.5f, 70f);

            Assert.That(detector.Process(first, 0f).Hits.Count, Is.EqualTo(1));
            Assert.That(detector.Process(scene.EmptyFrame(), 0.033f).Hits, Is.Empty);
            Assert.That(detector.Process(second, 0.066f).Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void 模擬深度のクリックも揺れの中で検知できる()
        {
            SimulatedDepthSource simulator = new SimulatedDepthSource(MountOrientation.Side, 42);
            BaselineRecorder recorder = new BaselineRecorder(simulator.Width, simulator.Height);
            ushort[] frame = new ushort[simulator.Width * simulator.Height];
            recorder.Begin();
            while (!recorder.IsComplete)
            {
                simulator.TryGetLatestFrame(frame);
                recorder.AddFrame(frame);
            }

            CalibrationData data = scene.CreateCalibration();
            data.SetBaseline(recorder.BuildMedian(), simulator.Width, simulator.Height);
            HitDetector noisyDetector = new HitDetector(new DetectionSettings());
            Assert.That(noisyDetector.Configure(data), Is.True);

            int hits = 0;
            for (int i = 0; i < 20; i++)
            {
                simulator.TryGetLatestFrame(frame);
                hits += noisyDetector.Process(frame, i * 0.033f).Hits.Count;
            }

            Assert.That(hits, Is.EqualTo(0), "揺れだけではヒットしない");
            simulator.InjectBall(new UnityEngine.Vector2(0.5f, 0.3f));
            simulator.TryGetLatestFrame(frame);
            DetectionResult result = noisyDetector.Process(frame, 1f);
            Assert.That(result.Hits.Count, Is.EqualTo(1));
            Assert.That(result.Hits[0].ImageUv.x, Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(result.Hits[0].ImageUv.y, Is.EqualTo(0.3f).Within(0.02f));
        }

        [Test]
        public void 揺れの大きい画素ではしきい値が上がり小さな変化を拾わない()
        {
            CalibrationData data = scene.CreateCalibration();
            ushort[] noise = new ushort[data.Baseline.Length];
            for (int i = 0; i < noise.Length; i++)
            {
                noise[i] = 40;
            }

            data.SetBaseline(data.Baseline, data.Width, data.Height, noise);
            HitDetector strict = new HitDetector(new DetectionSettings());
            Assert.That(strict.Configure(data), Is.True);
            int index = 100 * scene.Width + 160;
            Assert.That(strict.GetNoiseThreshold(index), Is.GreaterThan(45f));

            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 160f, 100f, 4.5f, 60f);
            Assert.That(detector.Process(frame, 0f).Hits.Count, Is.EqualTo(1), "揺れ幅が無ければ拾う");
            Assert.That(strict.Process(frame, 0f).Hits, Is.Empty, "揺れる画素では拾わない");
        }

        [Test]
        public void 基準を録る間に0になった画素は深度0でも候補にしない()
        {
            CalibrationData data = scene.CreateCalibration();
            ushort[] noise = new ushort[data.Baseline.Length];
            for (int i = 0; i < noise.Length; i++)
            {
                noise[i] = BaselineRecorder.UnstableNoise;
            }

            data.SetBaseline(data.Baseline, data.Width, data.Height, noise);
            HitDetector strict = new HitDetector(new DetectionSettings());
            Assert.That(strict.Configure(data), Is.True);
            ushort[] frame = scene.EmptyFrame();
            scene.DrawDisc(frame, 160f, 120f, 4.5f, -1f);

            Assert.That(strict.Process(frame, 0f).Hits, Is.Empty);
            Assert.That(strict.Process(frame, 0f).ChangedPixelCount, Is.EqualTo(0));
        }
    }
}
