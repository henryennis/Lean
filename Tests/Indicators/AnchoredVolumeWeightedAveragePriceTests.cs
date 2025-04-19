/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QuantConnect.Indicators;
using QuantConnect.Data.Market;
using QuantConnect.Data;
using System.Xml;
namespace QuantConnect.Tests.Indicators
{
    [TestFixture, Parallelizable(ParallelScope.Fixtures)]
    public class AnchoredVolumeWeightedAveragePriceTests : CommonIndicatorTests<TradeBar>
    {
        private static readonly DateTime DefaultAnchor = new DateTime(2024, 01, 15, 0, 0, 0, DateTimeKind.Utc);

        protected override IndicatorBase<TradeBar> CreateIndicator()
        {
            return new AnchoredVolumeWeightedAveragePrice(DefaultAnchor);
        }

        protected override string TestFileName => "spy_with_vwap.txt";

        protected override string TestColumnName => "Moving VWAP 50";

        [Test]
        public override void ComparesAgainstExternalData()
        {
            // This test relies on TestFileName and TestColumnName, which are not applicable
            // to the anchored indicator whose output depends on the anchor point.
            Assert.Ignore("Comparison against external data is not applicable for Anchored VWAP.");
        }

        [Test]
        public override void ComparesAgainstExternalDataAfterReset()
        {
            // Similar reason as ComparesAgainstExternalData.
            Assert.Ignore("Comparison against external data after reset is not applicable for Anchored VWAP.");
        }

        [Test]
        public override void WarmsUpProperly()
        {
            // Anchored VWAP doesn't have a fixed WarmUpPeriod in the traditional sense.
            // It's ready after the first update *after* the anchor time.
            var anchor = new DateTime(2024, 1, 15);
            var indicator = new AnchoredVolumeWeightedAveragePrice(anchor);

            Assert.IsFalse(indicator.IsReady);

            // Update before anchor
            indicator.Update(new TradeBar(anchor.AddDays(-1), Symbols.SPY, 1m, 1m, 1m, 1m, 1));
            Assert.IsFalse(indicator.IsReady);

            // Update at or after anchor
            indicator.Update(new TradeBar(anchor, Symbols.SPY, 1m, 1m, 1m, 1m, 1));
            Assert.IsTrue(indicator.IsReady);
            Assert.AreEqual(1, indicator.Samples); // Only counts samples at or after the anchor
        }

        [Test]
        public override void WarmUpIndicatorProducesConsistentResults()
        {
            // The concept of warming up with a fixed history period before the start date
            // doesn't directly apply as the anchor point defines the start of the calculation window.
            Assert.Ignore("WarmUpIndicator test is not applicable for Anchored VWAP.");
        }

        [Test]
        public void IsReadyAfterPeriodUpdates()
        {
            var indicator = CreateIndicator();
            Assert.IsFalse(indicator.IsReady);
            indicator.Update(new TradeBar(DateTime.UtcNow, Symbols.SPY, 1m, 1m, 1m, 1m, 1));
            Assert.IsTrue(indicator.IsReady);
        }

        [Test]
        public override void ResetsProperly()
        {
            var indicator = CreateIndicator();

            foreach (var data in TestHelper.GetTradeBarStream(TestFileName))
            {
                indicator.Update(data);
                Assert.IsTrue(indicator.IsReady);
            }
            Assert.IsTrue(indicator.IsReady);

            indicator.Reset();

            TestHelper.AssertIndicatorIsInDefaultState(indicator);
            indicator.Update(new TradeBar(DateTime.UtcNow, Symbols.SPY, 2m, 2m, 2m, 2m, 1));
            Assert.AreEqual(indicator.Current.Value, 2m);
        }

        [Test]
        public void MetamorphicPriceScalingTest()
        {
            var anchor = new DateTime(2024, 5, 10, 9, 30, 0, DateTimeKind.Utc);
            var indicator1 = new AnchoredVolumeWeightedAveragePrice(anchor);
            var indicator2 = new AnchoredVolumeWeightedAveragePrice(anchor);

            const decimal scaleFactor = 2.0m;
            var data = new[]
            {
                new TradeBar(anchor.AddMinutes(1), Symbols.SPY, 10m, 11m, 9m, 10.5m, 100),
                new TradeBar(anchor.AddMinutes(2), Symbols.SPY, 11m, 12m, 10m, 11.5m, 150),
                new TradeBar(anchor.AddMinutes(3), Symbols.SPY, 12m, 13m, 11m, 12.5m, 200)
            };

            foreach (var bar in data)
            {
                indicator1.Update(bar);

                // Create scaled bar
                var scaledBar = new TradeBar(bar.Time, bar.Symbol,
                    bar.Open * scaleFactor, bar.High * scaleFactor, bar.Low * scaleFactor, bar.Close * scaleFactor,
                    bar.Volume);
                // Use TradeBar.Price which is Close for AVWAP calculation
                scaledBar.Value = bar.Price * scaleFactor;
                indicator2.Update(scaledBar);

                Assert.IsTrue(indicator1.IsReady);
                Assert.IsTrue(indicator2.IsReady);
                Assert.That(indicator2.Current.Value, Is.EqualTo(indicator1.Current.Value * scaleFactor).Within(1e-10m),
                    $"Mismatch at time {bar.Time}");
            }
        }

        [Test]
        public void MetamorphicVolumeScalingTest()
        {
            var anchor = new DateTime(2024, 5, 10, 9, 30, 0, DateTimeKind.Utc);
            var indicator1 = new AnchoredVolumeWeightedAveragePrice(anchor);
            var indicator2 = new AnchoredVolumeWeightedAveragePrice(anchor);

            const decimal scaleFactor = 10m;
            var data = new[]
            {
                new TradeBar(anchor.AddMinutes(1), Symbols.SPY, 10m, 11m, 9m, 10.5m, 100),
                new TradeBar(anchor.AddMinutes(2), Symbols.SPY, 11m, 12m, 10m, 11.5m, 150),
                new TradeBar(anchor.AddMinutes(3), Symbols.SPY, 12m, 13m, 11m, 12.5m, 200)
            };

            foreach (var bar in data)
            {
                indicator1.Update(bar);

                // Create scaled volume bar
                var scaledBar = new TradeBar(bar.Time, bar.Symbol,
                    bar.Open, bar.High, bar.Low, bar.Close,
                    (long)(bar.Volume * scaleFactor)); // Volume must be long
                scaledBar.Value = bar.Price;
                indicator2.Update(scaledBar);

                Assert.IsTrue(indicator1.IsReady);
                Assert.IsTrue(indicator2.IsReady);
                Assert.That(indicator2.Current.Value, Is.EqualTo(indicator1.Current.Value).Within(1e-10m),
                    $"Mismatch at time {bar.Time}");
            }
        }

        [Test]
        public void MetamorphicTimeShiftingTest()
        {
            var anchor1 = new DateTime(2024, 5, 10, 9, 30, 0, DateTimeKind.Utc);
            var timeShift = TimeSpan.FromDays(5);
            var anchor2 = anchor1 + timeShift;

            var indicator1 = new AnchoredVolumeWeightedAveragePrice(anchor1);
            var indicator2 = new AnchoredVolumeWeightedAveragePrice(anchor2);

            var data = new[]
            {
                new TradeBar(anchor1.AddMinutes(1), Symbols.SPY, 10m, 11m, 9m, 10.5m, 100),
                new TradeBar(anchor1.AddMinutes(2), Symbols.SPY, 11m, 12m, 10m, 11.5m, 150),
                new TradeBar(anchor1.AddMinutes(3), Symbols.SPY, 12m, 13m, 11m, 12.5m, 200)
            };

            for (int i = 0; i < data.Length; i++)
            {
                var bar1 = data[i];
                indicator1.Update(bar1);

                // Create time-shifted bar
                var bar2 = new TradeBar(bar1.Time + timeShift, bar1.Symbol,
                    bar1.Open, bar1.High, bar1.Low, bar1.Close, bar1.Volume);
                bar2.Value = bar1.Price;
                indicator2.Update(bar2);

                Assert.IsTrue(indicator1.IsReady);
                Assert.IsTrue(indicator2.IsReady);
                Assert.That(indicator2.Current.Value, Is.EqualTo(indicator1.Current.Value).Within(1e-10m),
                    $"Mismatch at index {i}");
                Assert.AreEqual(indicator1.Current.Time + timeShift, indicator2.Current.Time);
            }
        }

        [Test]
        public void MetamorphicZeroVolumeDataTest()
        {
            var anchor = new DateTime(2024, 5, 10, 9, 30, 0, DateTimeKind.Utc);
            var indicator1 = new AnchoredVolumeWeightedAveragePrice(anchor);
            var indicator2 = new AnchoredVolumeWeightedAveragePrice(anchor);

            var data = new[]
            {
                new TradeBar(anchor.AddMinutes(1), Symbols.SPY, 10m, 11m, 9m, 10.5m, 100),
                new TradeBar(anchor.AddMinutes(2), Symbols.SPY, 11m, 12m, 10m, 11.5m, 150),
                new TradeBar(anchor.AddMinutes(3), Symbols.SPY, 12m, 13m, 11m, 12.5m, 200)
            };

            var zeroVolumeBar1 = new TradeBar(anchor.AddMinutes(1.5), Symbols.SPY, 99m, 99m, 99m, 99m, 0);
            var zeroVolumeBar2 = new TradeBar(anchor.AddMinutes(2.5), Symbols.SPY, 88m, 88m, 88m, 88m, 0);

            int dataIndex = 0;
            indicator1.Update(data[dataIndex]);
            indicator2.Update(data[dataIndex]);
            dataIndex++;

            // Add zero volume data to indicator2 only
            indicator2.Update(zeroVolumeBar1);
            Assert.That(indicator2.Current.Value, Is.EqualTo(indicator1.Current.Value).Within(1e-10m), "Mismatch after zero volume bar");

            indicator1.Update(data[dataIndex]);
            indicator2.Update(data[dataIndex]);
            dataIndex++;
            Assert.That(indicator2.Current.Value, Is.EqualTo(indicator1.Current.Value).Within(1e-10m), "Mismatch after second bar");

            // Add zero volume data to indicator2 only
            indicator2.Update(zeroVolumeBar2);
            Assert.That(indicator2.Current.Value, Is.EqualTo(indicator1.Current.Value).Within(1e-10m), "Mismatch after zero volume bar");

            indicator1.Update(data[dataIndex]);
            indicator2.Update(data[dataIndex]);
            Assert.That(indicator2.Current.Value, Is.EqualTo(indicator1.Current.Value).Within(1e-10m), "Mismatch after third bar");
        }

        [Test]
        public void AnchoringBehaviorTest()
        {
            // Replacing the MetamorphicReAnchoringInvarianceTest since anchors can't be changed
            // Test that data before anchor is ignored
            var anchorTime = new DateTime(2024, 5, 10, 9, 30, 0, DateTimeKind.Utc);
            var indicator = new AnchoredVolumeWeightedAveragePrice(anchorTime);

            var dataBefore = new[]
            {
                new TradeBar(anchorTime.AddMinutes(-2), Symbols.SPY, 9m, 9m, 9m, 9m, 50),
                new TradeBar(anchorTime.AddMinutes(-1), Symbols.SPY, 9.5m, 9.5m, 9.5m, 9.5m, 75)
            };

            var dataAfter = new[]
            {
                new TradeBar(anchorTime, Symbols.SPY, 10m, 11m, 9m, 10.5m, 100),
                new TradeBar(anchorTime.AddMinutes(1), Symbols.SPY, 11m, 12m, 10m, 11.5m, 150),
                new TradeBar(anchorTime.AddMinutes(2), Symbols.SPY, 12m, 13m, 11m, 12.5m, 200)
            };

            // Process data before anchor - should be ignored
            foreach (var bar in dataBefore)
            {
                indicator.Update(bar);
            }
            Assert.IsFalse(indicator.IsReady); // Still not ready
            Assert.AreEqual(0, indicator.Samples); // No samples counted

            // Process data at/after anchor - should be included
            foreach (var bar in dataAfter)
            {
                indicator.Update(bar);
            }

            Assert.IsTrue(indicator.IsReady);
            Assert.AreEqual(3, indicator.Samples); // Only the data after anchor
        }
    }
}