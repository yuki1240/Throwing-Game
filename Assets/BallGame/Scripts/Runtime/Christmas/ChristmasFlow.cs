using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// クリスマスの場面の進み具合。
    /// </summary>
    public enum ChristmasPhase
    {
        /// <summary>ボールを当てて飾り付けをしている。</summary>
        Playing = 0,

        /// <summary>「クリスマスツリーの完成～！」を出している。</summary>
        Celebrating = 1,

        /// <summary>次の場面へのカウントダウン中。</summary>
        Countdown = 2,

        /// <summary>次の場面へ移ってよい。</summary>
        Finished = 3
    }

    /// <summary>
    /// クリスマスの場面の流れ（15回当てる → 完成を5秒 → カウントダウン3秒 → 次の場面）を管理します。
    /// 画面に依存しないので、テストで確かめられます。
    /// </summary>
    public sealed class ChristmasFlow
    {
        /// <summary>ツリーが完成するまでに当てる回数。</summary>
        public const int DefaultGoal = 15;

        /// <summary>「完成～！」を出しておく秒数。</summary>
        public const float DefaultCelebrateSeconds = 5f;

        /// <summary>カウントダウンの始まりの数。</summary>
        public const int DefaultCountdownFrom = 3;

        private readonly int goal;
        private readonly float celebrateSeconds;
        private readonly int countdownFrom;
        private ChristmasPhase phase = ChristmasPhase.Playing;
        private int hitCount;
        private float phaseElapsed;

        /// <summary>
        /// 流れを作ります。
        /// </summary>
        /// <param name="goal">完成までに当てる回数</param>
        /// <param name="celebrateSeconds">完成を出す秒数</param>
        /// <param name="countdownFrom">カウントダウンの始まりの数</param>
        public ChristmasFlow(int goal = DefaultGoal, float celebrateSeconds = DefaultCelebrateSeconds, int countdownFrom = DefaultCountdownFrom)
        {
            this.goal = Mathf.Max(1, goal);
            this.celebrateSeconds = Mathf.Max(0f, celebrateSeconds);
            this.countdownFrom = Mathf.Max(1, countdownFrom);
        }

        /// <summary>今の進み具合。</summary>
        public ChristmasPhase Phase
        {
            get { return phase; }
        }

        /// <summary>当てた回数（飾りの数）。</summary>
        public int HitCount
        {
            get { return hitCount; }
        }

        /// <summary>完成までに当てる回数。</summary>
        public int Goal
        {
            get { return goal; }
        }

        /// <summary>完成まであと何回か。</summary>
        public int Remaining
        {
            get { return goal - hitCount; }
        }

        /// <summary>
        /// 当たったことを知らせます。飾り付け中だけ数えます。
        /// </summary>
        /// <returns>数えたとき true（新しい飾りを付ける）</returns>
        public bool RegisterHit()
        {
            if (phase != ChristmasPhase.Playing)
            {
                return false;
            }

            hitCount++;
            if (hitCount >= goal)
            {
                ChangePhase(ChristmasPhase.Celebrating);
            }

            return true;
        }

        /// <summary>
        /// 時間を進めます。完成を出し終えたらカウントダウン、数え終えたら終わりにします。
        /// </summary>
        /// <param name="deltaSeconds">進める秒</param>
        public void Tick(float deltaSeconds)
        {
            if (phase == ChristmasPhase.Playing || phase == ChristmasPhase.Finished)
            {
                return;
            }

            phaseElapsed += deltaSeconds;
            if (phase == ChristmasPhase.Celebrating && phaseElapsed >= celebrateSeconds)
            {
                ChangePhase(ChristmasPhase.Countdown);
            }
            else if (phase == ChristmasPhase.Countdown && phaseElapsed >= countdownFrom)
            {
                ChangePhase(ChristmasPhase.Finished);
            }
        }

        /// <summary>
        /// カウントダウンで今出す数（3, 2, 1）を返します。カウントダウン中でなければ 0 です。
        /// </summary>
        /// <returns>出す数</returns>
        public int GetCountdownNumber()
        {
            if (phase != ChristmasPhase.Countdown)
            {
                return 0;
            }

            return Mathf.Clamp(countdownFrom - Mathf.FloorToInt(phaseElapsed), 1, countdownFrom);
        }

        /// <summary>
        /// 今の場面に入ってからの秒数を返します（演出の動きに使う）。
        /// </summary>
        /// <returns>秒</returns>
        public float GetPhaseElapsed()
        {
            return phaseElapsed;
        }

        private void ChangePhase(ChristmasPhase next)
        {
            phase = next;
            phaseElapsed = 0f;
        }
    }
}
