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
        private static readonly DateTime DefaultAnchor = new DateTime(1998, 01, 15, 0, 0, 0, DateTimeKind.Utc);

        protected override IndicatorBase<TradeBar> CreateIndicator()
        {
            return new AnchoredVolumeWeightedAveragePrice(DefaultAnchor);
        }

        protected override string TestFileName => "spy_with_vwap.txt";

        // This is not used for Anchored VWAP tests that depend on the anchor point.
        protected override string TestColumnName => "";


        [Test]
        /// <summary>
        /// The final value of this indicator is zero because it uses the Volume of the bars it receives.
        /// Since RenkoBar's don't always have Volume, the final current value is zero. Therefore we
        /// skip this test
        /// </summary>
        /// <param name="indicator"></param>
        protected override void IndicatorValueIsNotZeroAfterReceiveRenkoBars(IndicatorBase indicator)
        {
        }


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
            // It's ready after the first update, regardless of anchor time.
            // Calculation only includes data at or after the anchor time.
            var anchor = new DateTime(2024, 1, 15);
            var indicator = new AnchoredVolumeWeightedAveragePrice(anchor);
            var preAnchorTime = anchor.AddDays(-1);
            var anchorTime = anchor;
            var postAnchorTime = anchor.AddDays(1);

            Assert.IsFalse(indicator.IsReady);
            Assert.AreEqual(0, indicator.Samples);
            Assert.AreEqual(0m, indicator.Current.Value);

            // Update before anchor
            var bar1 = new TradeBar(preAnchorTime, Symbols.SPY, 10m, 11m, 9m, 10.5m, 100);
            indicator.Update(bar1);
            Assert.IsTrue(indicator.IsReady); // IsReady is true after the first update
            Assert.AreEqual(1, indicator.Samples); // Samples count all updates
            Assert.AreEqual(0m, indicator.Current.Value); // Value remains 0 as data is before anchor

            // Update at anchor
            var bar2 = new TradeBar(anchorTime, Symbols.SPY, 11m, 12m, 10m, 11.5m, 150);
            indicator.Update(bar2);
            Assert.IsTrue(indicator.IsReady);
            Assert.AreEqual(2, indicator.Samples);
            // Value is calculated using only bar2: (11+12+10+11.5)/4 = 11.125
            Assert.AreEqual(11.125m, indicator.Current.Value);

            // Update after anchor
            var bar3 = new TradeBar(postAnchorTime, Symbols.SPY, 12m, 13m, 11m, 12.5m, 200);
            indicator.Update(bar3);
            Assert.IsTrue(indicator.IsReady);
            Assert.AreEqual(3, indicator.Samples);
            // Value is calculated using bar2 and bar3
            // Price2 = 11.125, Volume2 = 150
            // Price3 = (12+13+11+12.5)/4 = 12.125, Volume3 = 200
            // SumPV = (11.125 * 150) + (12.125 * 200) = 1668.75 + 2425 = 4093.75
            // SumV = 150 + 200 = 350
            // VWAP = 4093.75 / 350 = 11.69642857...
            Assert.AreEqual(4093.75m / 350m, indicator.Current.Value);
        }


        [Test]
        public void IsReadyAfterFirstUpdate()
        {
            var indicator = CreateIndicator() as AnchoredVolumeWeightedAveragePrice;
            Assert.IsFalse(indicator.IsReady);
            // Use a time before the default anchor
            indicator.Update(new TradeBar(DefaultAnchor.AddDays(-1), Symbols.SPY, 1m, 1m, 1m, 1m, 1));
            Assert.IsTrue(indicator.IsReady); // Should be ready after the first update
            Assert.AreEqual(0m, indicator.Current.Value); // Value should be 0 as update is before anchor
        }

        [Test]
        public override void ResetsProperly()
        {
            var indicator = CreateIndicator() as AnchoredVolumeWeightedAveragePrice;
            var anchor = indicator.Anchor;

            // Update with data before and after anchor
            indicator.Update(new TradeBar(anchor.AddMinutes(-1), Symbols.SPY, 1m, 1m, 1m, 1m, 100));
            indicator.Update(new TradeBar(anchor.AddMinutes(1), Symbols.SPY, 2m, 2m, 2m, 2m, 100));
            indicator.Update(new TradeBar(anchor.AddMinutes(2), Symbols.SPY, 3m, 3m, 3m, 3m, 100));

            Assert.IsTrue(indicator.IsReady);
            Assert.AreNotEqual(0, indicator.Samples);
            Assert.AreNotEqual(0m, indicator.Current.Value); // Should have a calculated value

            indicator.Reset();

            TestHelper.AssertIndicatorIsInDefaultState(indicator);
            Assert.IsFalse(indicator.IsReady); // IsReady should be false after reset

            // Update with data after the anchor
            var bar = new TradeBar(anchor.AddMinutes(5), Symbols.SPY, 5m, 5m, 5m, 5m, 100);
            indicator.Update(bar);
            Assert.IsTrue(indicator.IsReady);
            // Expected value is the TWAP of the first bar after reset (and after anchor)
            // TWAP = (5+5+5+5)/4 = 5
            Assert.AreEqual(5m, indicator.Current.Value);
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
                // Ensure data is after the anchor
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
                // Value (Close price) is used in the default TWAP calculation
                scaledBar.Value = bar.Value * scaleFactor;
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
                 // Ensure data is after the anchor
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
                scaledBar.Value = bar.Value; // Keep price the same
                indicator2.Update(scaledBar);

                Assert.IsTrue(indicator1.IsReady);
                Assert.IsTrue(indicator2.IsReady);
                // VWAP should remain the same if only volume is scaled proportionally
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
                 // Ensure data is after the anchor
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
                bar2.Value = bar1.Value;
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
                 // Ensure data is after the anchor
                new TradeBar(anchor.AddMinutes(1), Symbols.SPY, 10m, 11m, 9m, 10.5m, 100),
                new TradeBar(anchor.AddMinutes(2), Symbols.SPY, 11m, 12m, 10m, 11.5m, 150),
                new TradeBar(anchor.AddMinutes(3), Symbols.SPY, 12m, 13m, 11m, 12.5m, 200)
            };

            // Ensure zero volume bars are also after the anchor
            var zeroVolumeBar1 = new TradeBar(anchor.AddMinutes(1.5), Symbols.SPY, 99m, 99m, 99m, 99m, 0);
            var zeroVolumeBar2 = new TradeBar(anchor.AddMinutes(2.5), Symbols.SPY, 88m, 88m, 88m, 88m, 0);

            int dataIndex = 0;
            indicator1.Update(data[dataIndex]);
            indicator2.Update(data[dataIndex]);
            dataIndex++;
            var lastVal1 = indicator1.Current.Value;
            var lastVal2 = indicator2.Current.Value;

            // Add zero volume data to indicator2 only
            indicator2.Update(zeroVolumeBar1);
            // Zero volume bars should not change the VWAP value
            Assert.That(indicator2.Current.Value, Is.EqualTo(lastVal1).Within(1e-10m), "Mismatch after zero volume bar 1");
            Assert.AreEqual(lastVal2, indicator2.Current.Value); // Value should be unchanged

            indicator1.Update(data[dataIndex]);
            indicator2.Update(data[dataIndex]);
            dataIndex++;
            Assert.That(indicator2.Current.Value, Is.EqualTo(indicator1.Current.Value).Within(1e-10m), "Mismatch after second data bar");
            lastVal1 = indicator1.Current.Value;
            lastVal2 = indicator2.Current.Value;


            // Add zero volume data to indicator2 only
            indicator2.Update(zeroVolumeBar2);
            Assert.That(indicator2.Current.Value, Is.EqualTo(lastVal1).Within(1e-10m), "Mismatch after zero volume bar 2");
            Assert.AreEqual(lastVal2, indicator2.Current.Value); // Value should be unchanged

            indicator1.Update(data[dataIndex]);
            indicator2.Update(data[dataIndex]);
            Assert.That(indicator2.Current.Value, Is.EqualTo(indicator1.Current.Value).Within(1e-10m), "Mismatch after third data bar");
        }


    }
}