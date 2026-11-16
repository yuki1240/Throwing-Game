using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BallGame.Tests
{
    /// <summary>
    /// クリスマスの場面（飾り付け → 完成 → カウントダウン）のテスト。
    /// </summary>
    public sealed class ChristmasTests
    {
        [Test]
        public void 十五回当てるとツリーが完成する()
        {
            ChristmasFlow flow = new ChristmasFlow();
            for (int i = 0; i < 14; i++)
            {
                Assert.That(flow.RegisterHit(), Is.True);
            }

            Assert.That(flow.Phase, Is.EqualTo(ChristmasPhase.Playing));
            Assert.That(flow.Remaining, Is.EqualTo(1));
            Assert.That(flow.RegisterHit(), Is.True);
            Assert.That(flow.Phase, Is.EqualTo(ChristmasPhase.Celebrating));
            Assert.That(flow.HitCount, Is.EqualTo(15));
        }

        [Test]
        public void 完成のあとに当てても数えない()
        {
            ChristmasFlow flow = CreateCompletedFlow();

            Assert.That(flow.RegisterHit(), Is.False);
            Assert.That(flow.HitCount, Is.EqualTo(15));
        }

        [Test]
        public void 完成を五秒出してからカウントダウンする()
        {
            ChristmasFlow flow = CreateCompletedFlow();

            flow.Tick(4.9f);
            Assert.That(flow.Phase, Is.EqualTo(ChristmasPhase.Celebrating));
            Assert.That(flow.GetCountdownNumber(), Is.EqualTo(0));
            flow.Tick(0.2f);
            Assert.That(flow.Phase, Is.EqualTo(ChristmasPhase.Countdown));
        }

        [Test]
        public void カウントダウンは三二一と進んで終わる()
        {
            ChristmasFlow flow = CreateCompletedFlow();
            flow.Tick(5f);
            List<int> numbers = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                numbers.Add(flow.GetCountdownNumber());
                flow.Tick(0.5f);
            }

            Assert.That(numbers, Is.EqualTo(new[] { 3, 3, 2, 2, 1, 1 }));
            Assert.That(flow.Phase, Is.EqualTo(ChristmasPhase.Finished));
        }

        [Test]
        public void 飾り付け中は時間がたっても進まない()
        {
            ChristmasFlow flow = new ChristmasFlow();
            flow.RegisterHit();
            flow.Tick(100f);

            Assert.That(flow.Phase, Is.EqualTo(ChristmasPhase.Playing));
        }

        [Test]
        public void 飾りの場所は十五か所でツリーの中にある()
        {
            List<Vector2> slots = ChristmasTreeLayout.GetOrnamentSlots();

            Assert.That(slots.Count, Is.EqualTo(ChristmasFlow.DefaultGoal));
            foreach (Vector2 slot in slots)
            {
                float halfWidth = ChristmasTreeLayout.GetHalfWidthAt(slot.y);
                Assert.That(Mathf.Abs(slot.x - ChristmasTreeLayout.CenterX), Is.LessThan(halfWidth), slot.ToString());
                Assert.That(slot.y, Is.InRange(ChristmasTreeLayout.BaseY, ChristmasTreeLayout.ApexY));
            }

            for (int i = 0; i < slots.Count; i++)
            {
                for (int j = i + 1; j < slots.Count; j++)
                {
                    Assert.That(Vector2.Distance(slots[i], slots[j]), Is.GreaterThan(80f), "飾りが重ならない");
                }
            }
        }

        [Test]
        public void 飾りを付ける順番は全部の場所を一度ずつ使う()
        {
            int[] order = ChristmasTreeLayout.GetShuffledOrder(15, new System.Random(4));
            List<int> sorted = new List<int>(order);
            sorted.Sort();

            Assert.That(sorted, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 }));
        }

        [Test]
        public void 飾りは当たった所から出てツリーの場所に着く()
        {
            Vector2 from = new Vector2(100f, 900f);
            Vector2 to = new Vector2(960f, 500f);
            Vector2 via = new Vector2(530f, 960f);

            Assert.That(OrnamentFlight.EvaluateArc(from, via, to, 0f), Is.EqualTo(from));
            Assert.That(OrnamentFlight.EvaluateArc(from, via, to, 1f), Is.EqualTo(to));
        }

        private static ChristmasFlow CreateCompletedFlow()
        {
            ChristmasFlow flow = new ChristmasFlow();
            for (int i = 0; i < ChristmasFlow.DefaultGoal; i++)
            {
                flow.RegisterHit();
            }

            return flow;
        }
    }
}
