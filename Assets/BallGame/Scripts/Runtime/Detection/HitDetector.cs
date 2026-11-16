using System.Collections.Generic;
using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 基準深度と現在の深度を比べて、壁に当たったボールを見つけます。
    /// Kinect は壁に沿って見るため、奥行きの差ではなく「四隅の基準から作った壁の面までの垂直距離」で判断します。
    /// </summary>
    public sealed class HitDetector
    {
        private sealed class Track
        {
            public Vector2 Position;
            public int LastFrame;
            public int ConsecutiveFrames;
            public bool Emitted;
        }

        private readonly DetectionSettings settings;
        private readonly DetectionResult result = new DetectionResult();
        private readonly List<Track> tracks = new List<Track>();
        private readonly Queue<int> floodQueue = new Queue<int>();

        private int width;
        private int height;
        private ushort[] baseline;
        private bool[] insideMask;
        private float[] normalDotRay;
        private float[] baselineDistance;
        private bool[] candidate;
        private bool[] filtered;
        private bool[] visited;
        private WallPlane plane;
        private bool isConfigured;
        private int frameIndex;
        private float lastPersonTime = float.NegativeInfinity;

        /// <summary>
        /// 検知器を作ります。
        /// </summary>
        /// <param name="settings">しきい値</param>
        public HitDetector(DetectionSettings settings)
        {
            this.settings = settings ?? new DetectionSettings();
        }

        /// <summary>しきい値。</summary>
        public DetectionSettings Settings
        {
            get { return settings; }
        }

        /// <summary>校正を取り込んで検知できる状態か。</summary>
        public bool IsConfigured
        {
            get { return isConfigured; }
        }

        /// <summary>壁の面。</summary>
        public WallPlane Plane
        {
            get { return plane; }
        }

        /// <summary>幅。</summary>
        public int Width
        {
            get { return width; }
        }

        /// <summary>高さ。</summary>
        public int Height
        {
            get { return height; }
        }

        /// <summary>
        /// 校正データ（四隅と基準深度）を取り込みます。
        /// </summary>
        /// <param name="data">校正データ</param>
        /// <returns>検知できる状態になったとき true</returns>
        public bool Configure(CalibrationData data)
        {
            isConfigured = false;
            ResetTracking();
            if (data == null || !data.IsComplete)
            {
                return false;
            }

            settings.ApplyCalibration(data);
            AllocateBuffers(data.Width, data.Height);
            baseline = data.Baseline;
            DepthIntrinsics intrinsics = DepthIntrinsics.ForResolution(width, height);
            plane = WallPlaneBuilder.Build(baseline, width, height, data.Corners, intrinsics);
            if (!plane.IsValid)
            {
                Debug.LogWarning("四隅の基準から壁の面を作れませんでした。四隅の位置か基準を録り直してください。");
                return false;
            }

            BuildInsideMask(data.Corners);
            BuildDistanceTables(intrinsics);
            isConfigured = true;
            return true;
        }

        /// <summary>
        /// 追跡中の物体と人の待ち時間を消します。
        /// </summary>
        public void ResetTracking()
        {
            tracks.Clear();
            frameIndex = 0;
            lastPersonTime = float.NegativeInfinity;
        }

        /// <summary>
        /// 1フレーム分の深度を調べます。
        /// </summary>
        /// <param name="depth">現在の深度（幅×高さ、mm、0 は無効）</param>
        /// <param name="timeSeconds">現在の時刻（秒）</param>
        /// <returns>検知結果（次の呼び出しで上書きされます）</returns>
        public DetectionResult Process(ushort[] depth, float timeSeconds)
        {
            result.Clear();
            frameIndex++;
            if (!isConfigured || depth == null || depth.Length != width * height)
            {
                return result;
            }

            ClassifyPixels(depth);
            RemoveIsolatedPixels();
            ExtractBlobs();
            result.CandidateMask = filtered;
            if (IsPersonFrame())
            {
                HandlePersonFrame(timeSeconds);
            }
            else if (timeSeconds - lastPersonTime < settings.PersonCooldownSeconds)
            {
                // 人が去ったあとの待ち時間：見えた物体は覚えるだけで出さない
                result.InCooldown = true;
                UpdateTracks(false);
            }
            else
            {
                UpdateTracks(true);
            }

            ForgetOldTracks();
            return result;
        }

        /// <summary>
        /// 画素の深度から壁の面までの距離（基準との差、mm）を返します。
        /// </summary>
        /// <param name="index">画素の番号</param>
        /// <param name="depthMm">深度（mm）</param>
        /// <returns>基準より壁から離れた距離（mm、カメラ側が正）</returns>
        public float GetDistanceFromWall(int index, ushort depthMm)
        {
            return normalDotRay[index] * depthMm + plane.Offset - baselineDistance[index];
        }

        /// <summary>
        /// 画素が四隅の内側かどうかを返します。
        /// </summary>
        /// <param name="index">画素の番号</param>
        /// <returns>内側なら true</returns>
        public bool IsInside(int index)
        {
            return insideMask != null && insideMask[index];
        }

        private void AllocateBuffers(int newWidth, int newHeight)
        {
            width = newWidth;
            height = newHeight;
            int count = width * height;
            insideMask = new bool[count];
            normalDotRay = new float[count];
            baselineDistance = new float[count];
            candidate = new bool[count];
            filtered = new bool[count];
            visited = new bool[count];
        }

        private void BuildInsideMask(IList<Vector2> corners)
        {
            Vector2[] polygon = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                polygon[i] = new Vector2(corners[i].x * width, corners[i].y * height);
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    insideMask[y * width + x] = IsPointInPolygon(new Vector2(x + 0.5f, y + 0.5f), polygon);
                }
            }
        }

        private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                bool crosses = (a.y > point.y) != (b.y > point.y);
                if (crosses && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private void BuildDistanceTables(DepthIntrinsics intrinsics)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    normalDotRay[index] = Vector3.Dot(plane.Normal, intrinsics.GetRay(x + 0.5f, y + 0.5f));
                    baselineDistance[index] = normalDotRay[index] * baseline[index] + plane.Offset;
                }
            }
        }

        private void ClassifyPixels(ushort[] depth)
        {
            int changed = 0;
            for (int i = 0; i < candidate.Length; i++)
            {
                candidate[i] = false;
                if (!insideMask[i] || baseline[i] == 0)
                {
                    continue;
                }

                ushort current = depth[i];
                if (current == 0)
                {
                    // 基準があった画素が 0 になった：赤外線が返らない物体として候補にする
                    candidate[i] = true;
                    changed++;
                    continue;
                }

                float distance = GetDistanceFromWall(i, current);
                if (distance < settings.NoiseFloorMm)
                {
                    continue;
                }

                changed++;
                candidate[i] = distance <= settings.MaxDistanceMm;
            }

            result.ChangedPixelCount = changed;
        }

        private void RemoveIsolatedPixels()
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    filtered[index] = candidate[index] && HasCandidateNeighbor(x, y);
                }
            }
        }

        private bool HasCandidateNeighbor(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if ((dx != 0 || dy != 0) && IsCandidate(x + dx, y + dy))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsCandidate(int x, int y)
        {
            return x >= 0 && y >= 0 && x < width && y < height && candidate[y * width + x];
        }

        private void ExtractBlobs()
        {
            System.Array.Clear(visited, 0, visited.Length);
            for (int i = 0; i < filtered.Length; i++)
            {
                if (filtered[i] && !visited[i])
                {
                    result.Blobs.Add(FloodFill(i));
                }
            }
        }

        private DepthBlob FloodFill(int start)
        {
            DepthBlob blob = new DepthBlob { MinX = int.MaxValue, MinY = int.MaxValue, MaxX = int.MinValue, MaxY = int.MinValue };
            long sumX = 0;
            long sumY = 0;
            floodQueue.Clear();
            floodQueue.Enqueue(start);
            visited[start] = true;
            while (floodQueue.Count > 0)
            {
                int index = floodQueue.Dequeue();
                int x = index % width;
                int y = index / width;
                AddPixelToBlob(ref blob, x, y);
                sumX += x;
                sumY += y;
                EnqueueNeighbor(x + 1, y);
                EnqueueNeighbor(x - 1, y);
                EnqueueNeighbor(x, y + 1);
                EnqueueNeighbor(x, y - 1);
            }

            blob.CentroidX = (float)sumX / blob.Area;
            blob.CentroidY = (float)sumY / blob.Area;
            return blob;
        }

        private static void AddPixelToBlob(ref DepthBlob blob, int x, int y)
        {
            blob.Area++;
            blob.MinX = Mathf.Min(blob.MinX, x);
            blob.MinY = Mathf.Min(blob.MinY, y);
            blob.MaxX = Mathf.Max(blob.MaxX, x);
            blob.MaxY = Mathf.Max(blob.MaxY, y);
        }

        private void EnqueueNeighbor(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            int index = y * width + x;
            if (filtered[index] && !visited[index])
            {
                visited[index] = true;
                floodQueue.Enqueue(index);
            }
        }

        private bool IsPersonFrame()
        {
            if (result.ChangedPixelCount > settings.PersonPixelTotal)
            {
                return true;
            }

            foreach (DepthBlob blob in result.Blobs)
            {
                if (blob.Width > settings.PersonMaxExtent || blob.Height > settings.PersonMaxExtent)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandlePersonFrame(float timeSeconds)
        {
            result.PersonDetected = true;
            lastPersonTime = timeSeconds;
            // 人がいる間に見えた物体は、去ったあとに残っていても出さない
            UpdateTracks(false);
        }

        private void UpdateTracks(bool allowEmit)
        {
            foreach (DepthBlob blob in result.Blobs)
            {
                if (blob.Area < settings.MinArea)
                {
                    continue;
                }

                Track track = MatchOrCreateTrack(blob);
                if (!allowEmit)
                {
                    track.Emitted = true;
                    continue;
                }

                TryEmit(track, blob);
            }
        }

        private Track MatchOrCreateTrack(DepthBlob blob)
        {
            Vector2 position = new Vector2(blob.CentroidX, blob.CentroidY);
            Track track = FindNearestTrack(position);
            if (track == null)
            {
                track = new Track { Position = position, LastFrame = frameIndex, ConsecutiveFrames = 1 };
                tracks.Add(track);
                return track;
            }

            track.ConsecutiveFrames = track.LastFrame == frameIndex - 1 ? track.ConsecutiveFrames + 1 : 1;
            track.LastFrame = frameIndex;
            track.Position = position;
            return track;
        }

        private Track FindNearestTrack(Vector2 position)
        {
            Track best = null;
            float bestDistance = settings.TrackMatchRadius;
            foreach (Track track in tracks)
            {
                if (track.LastFrame == frameIndex)
                {
                    continue;
                }

                float distance = Vector2.Distance(track.Position, position);
                if (distance <= bestDistance)
                {
                    best = track;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void TryEmit(Track track, DepthBlob blob)
        {
            if (track.Emitted)
            {
                // 同じ場所に留まった物体は出し続けない
                return;
            }

            bool accepted = blob.Area >= settings.InstantArea || track.ConsecutiveFrames >= settings.ConfirmFrames;
            if (!accepted)
            {
                return;
            }

            track.Emitted = true;
            result.Hits.Add(CreateHit(blob));
        }

        private DetectedHit CreateHit(DepthBlob blob)
        {
            Vector2 pixel = new Vector2(blob.CentroidX, blob.CentroidY);
            Vector2 uv = new Vector2((blob.CentroidX + 0.5f) / width, (blob.CentroidY + 0.5f) / height);
            return new DetectedHit { Pixel = pixel, ImageUv = uv, Area = blob.Area };
        }

        private void ForgetOldTracks()
        {
            tracks.RemoveAll(track => frameIndex - track.LastFrame > settings.TrackForgetFrames);
        }
    }
}
