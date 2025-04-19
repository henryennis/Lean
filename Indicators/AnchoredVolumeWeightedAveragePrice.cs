using System;
using QuantConnect.Data;
using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// Represents the Anchored Volume Weighted Average Price (AVWAP) indicator.
    /// This indicator calculates the volume-weighted average price of an asset,
    /// starting its accumulation from a specified anchor date and time.
    /// </summary>
    public class AnchoredVolumeWeightedAveragePrice
        : TradeBarIndicator, IIndicatorWarmUpPeriodProvider
    {
        private readonly DateTime _anchor;
        private decimal _sumVolume;
        private decimal _sumPriceVolume;

        /// <summary>
        /// Gets the anchor date and time from which the VWAP calculation begins.
        /// </summary>
        public DateTime Anchor => _anchor;

        /// <summary>
        /// Gets a flag indicating when the indicator is ready and fully initialized.
        /// True is returned when at least one data point after the anchor time has been processed.
        /// </summary>
        public override bool IsReady => Samples >= WarmUpPeriod;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready after the anchor time.
        /// For AVWAP, this is 1, meaning it's ready after the first valid data point post-anchor.
        /// </summary>
        public int WarmUpPeriod => 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnchoredVolumeWeightedAveragePrice"/> class
        /// with a specified name and anchor time.
        /// </summary>
        /// <param name="name">The name of this indicator.</param>
        /// <param name="anchor">The anchor date and time to start accumulating VWAP data.</param>
        public AnchoredVolumeWeightedAveragePrice(string name, DateTime anchor)
            : base(name)
        {
            _anchor = anchor;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AnchoredVolumeWeightedAveragePrice"/> class
        /// using a default name derived from the anchor time.
        /// </summary>
        /// <param name="anchor">The anchor date and time to start accumulating VWAP data.</param>
        public AnchoredVolumeWeightedAveragePrice(DateTime anchor)
            : this($"AnchoredVWAP({anchor:yyyyMMddHHmmss})", anchor)
        {
        }


        /// <summary>
        /// Resets this indicator to its initial state, clearing all accumulated data.
        /// </summary>
        public override void Reset()
        {
            _sumVolume = 0m;
            _sumPriceVolume = 0m;
            base.Reset();
        }

        /// <summary>
        /// Validates the input data point and computes the next value of the AVWAP.
        /// The input must be a <see cref="TradeBar"/> occurring at or after the anchor time.
        /// </summary>
        /// <param name="input">The input <see cref="TradeBar"/> data point to process.</param>
        /// <returns>An <see cref="IndicatorResult"/> containing the status and the calculated AVWAP value.</returns>
        protected override IndicatorResult ValidateAndComputeNextValue(TradeBar input)
        {
            // Input validation: Ensure input is not null and its end time is at or after the anchor time.
            if (input == null || input.EndTime < _anchor)
            {
                // Return zero value with invalid status if input is before the anchor or null.
                return new IndicatorResult(0m, IndicatorStatus.InvalidInput);
            }

            decimal volume = input.Volume;
            // Use a representative price for the bar interval.
            decimal price = GetTimeWeightedAveragePrice(input);

            // Accumulate volume and price * volume.
            _sumVolume += volume;
            _sumPriceVolume += price * volume;

            // Check for division by zero if total volume is zero.
            if (_sumVolume == 0m)
            {
                // Return zero value with math error status if total volume is zero.
                // Consider returning input.Price or Current.Value if appropriate, but 0 is safer for division by zero.
                return new IndicatorResult(0, IndicatorStatus.MathError);
            }

            // Calculate and return the AVWAP.
            return new IndicatorResult(_sumPriceVolume / _sumVolume, IndicatorStatus.Success);
        }

        /// <summary>
        /// Calculates a representative average price for the given trade bar interval.
        /// The default implementation uses the average of Open, High, Low, and Close (OHLC/4).
        /// This method can be overridden in derived classes to use a different price calculation method (e.g., HLC/3).
        /// </summary>
        /// <param name="input">The current <see cref="TradeBar"/> input.</param>
        /// <returns>An estimated average price for the trade bar's interval.</returns>
        protected virtual decimal GetTimeWeightedAveragePrice(TradeBar input)
        {
            // Calculate average price using OHLC/4.
            return (input.Open + input.High + input.Low + input.Close) / 4;
        }

        /// <summary>
        /// This method is not used because the logic is handled directly within <see cref="ValidateAndComputeNextValue"/>.
        /// Calling this method will result in a <see cref="NotImplementedException"/>.
        /// </summary>
        /// <param name="input">The input data.</param>
        /// <returns>Throws <see cref="NotImplementedException"/>.</returns>
        /// <exception cref="NotImplementedException">Thrown because this method is bypassed by overriding <see cref="ValidateAndComputeNextValue"/>.</exception>
        protected override decimal ComputeNextValue(TradeBar input)
        {
            // This method should not be called as ValidateAndComputeNextValue is overridden.
            throw new NotImplementedException(
                $"{nameof(AnchoredVolumeWeightedAveragePrice)}.{nameof(ComputeNextValue)} should never be invoked.");
        }

    }
}