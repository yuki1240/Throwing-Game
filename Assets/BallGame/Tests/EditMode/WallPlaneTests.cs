using NUnit.Framework;
using UnityEngine;

namespace BallGame.Tests
{
    /// <summary>
    /// 壁の面までの距離のテスト。
    /// </summary>
    public sealed class WallPlaneTests
    {
        [Test]
        public void 四点から面を作りカメラ側を正の距離にする()
        {
            WallPlane plane = WallPlane.FromCornerPoints(new[]
            {
                new Vector3(-500f, -400f, 2000f),
                new Vector3(500f, -400f, 2000f),
                new Vector3(500f, 400f, 2000f),
                new Vector3(-500f, 400f, 2000f)
            });

            Assert.That(plane.IsValid, Is.True);
            Assert.That(plane.DistanceTo(new Vector3(0f, 0f, 1900f)), Is.EqualTo(100f).Within(0.01f));
            Assert.That(plane.DistanceTo(new Vector3(300f, 100f, 2100f)), Is.EqualTo(-100f).Within(0.01f));
            Assert.That(plane.DistanceTo(Vector3.zero), Is.GreaterThan(0f));
        }

        [Test]
        public void 傾いた面でも垂直距離になる()
        {
            WallPlane plane = WallPlane.FromNormalAndOffset(new Vector3(0f, 1f, 0.75f), -780f);
            Vector3 onPlane = new Vector3(0f, 30f, 1000f);
            Vector3 normal = plane.Normal;

            Assert.That(plane.DistanceTo(onPlane), Is.EqualTo(0f).Within(0.01f));
            Assert.That(plane.DistanceTo(onPlane + normal * 120f), Is.EqualTo(120f).Within(0.01f));
        }

        [Test]
        public void 一直線の点からは面を作れない()
        {
            WallPlane plane = WallPlane.FromCornerPoints(new[]
            {
                new Vector3(0f, 0f, 1000f),
                new Vector3(0f, 0f, 1000f),
                new Vector3(0f, 0f, 1000f),
                new Vector3(0f, 0f, 1000f)
            });

            Assert.That(plane.IsValid, Is.False);
        }

        [Test]
        public void 壁に沿って見ても奥行きでなく壁からの距離で測る()
        {
            TestDepthScene scene = new TestDepthScene();
            HitDetector detector = scene.CreateDetector();
            Assert.That(detector.IsConfigured, Is.True);

            foreach (int row in new[] { 30, 120, 200 })
            {
                int index = row * scene.Width + 160;
                ushort depth = (ushort)Mathf.RoundToInt(scene.Simulator.GetDepthAtWallDistance(row, 100f));
                Assert.That(detector.GetDistanceFromWall(index, depth), Is.EqualTo(100f).Within(8f), "行 " + row);
                Assert.That(detector.GetDistanceFromWall(index, scene.Baseline[index]), Is.EqualTo(0f).Within(0.01f));
            }
        }

        [Test]
        public void 同じ壁からの距離でも遠い行ほど奥行きの差が大きい()
        {
            TestDepthScene scene = new TestDepthScene();
            float nearDifference = scene.Simulator.GetWallDepthMm(220) - scene.Simulator.GetDepthAtWallDistance(220, 100f);
            float farDifference = scene.Simulator.GetWallDepthMm(20) - scene.Simulator.GetDepthAtWallDistance(20, 100f);

            Assert.That(farDifference, Is.GreaterThan(nearDifference * 2f));
        }

        [Test]
        public void 基準から壁の面を作れる()
        {
            TestDepthScene scene = new TestDepthScene();
            CalibrationData data = scene.CreateCalibration();
            WallPlane plane = WallPlaneBuilder.Build(data.Baseline, data.Width, data.Height, data.Corners, DepthIntrinsics.ForResolution(data.Width, data.Height));
            Vector3 expectedNormal = new Vector3(0f, -1f, -0.75f).normalized;

            Assert.That(plane.IsValid, Is.True);
            Assert.That(Vector3.Dot(plane.Normal, expectedNormal), Is.GreaterThan(0.999f));
        }
    }
}
