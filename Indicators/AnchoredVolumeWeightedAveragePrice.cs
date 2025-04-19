using System;
using System.Reflection.Metadata.Ecma335;
using QuantConnect.Data;
using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// Anchored Volume Weighted Average Price (VWAP) indicator.
    /// Starts accumulating from the specified anchor time.
    /// </summary>
    public class AnchoredVolumeWeightedAveragePrice 
        : TradeBarIndicator, IIndicatorWarmUpPeriodProvider
    {
        private readonly DateTime _anchor;
        private decimal _sumVolume;
        private decimal _sumPriceVolume;
    
        /// <summary>
        /// Gets the current anchor time.
        /// </summary>
        public DateTime Anchor => _anchor;

        /// <summary>
        /// True when at least one trade/bar since anchor has been processed.
        /// </summary>
        public override bool IsReady => _sumVolume > 0;

        /// <summary>
        /// No pre‐warmup required beyond the first valid sample.
        /// </summary>
        public int WarmUpPeriod => 1;

        /// <summary>
        /// Creates a new Anchored VWAP with the given name and anchor time.
        /// </summary>
        public AnchoredVolumeWeightedAveragePrice(string name, DateTime anchor)
            : base(name)
        {
            _anchor = anchor;
        }

        /// <summary>
        /// Creates a new Anchored VWAP using a default name derived from the anchor.
        /// </summary>
        public AnchoredVolumeWeightedAveragePrice(DateTime anchor)
            : this($"AnchoredVWAP({anchor:yyyyMMddHHmmss})", anchor)
        {
        }


        /// <summary>
        /// Clears all internal accumulators.
        /// </summary>
        public override void Reset()
        {
            _sumVolume = 0m;
            _sumPriceVolume = 0m;
            base.Reset();
        }

        /// <summary>
        /// Validates input (must be trade tick or non‐fill‐forward TradeBar after anchor)
        /// and updates VWAP accumulators.
        /// </summary>
        protected override IndicatorResult ValidateAndComputeNextValue(TradeBar input)
        {
            if (input == null || input.EndTime < _anchor)
            {
                return new IndicatorResult(0m, IndicatorStatus.InvalidInput);
            }

            decimal volume, price;

            volume = input.Volume;
            price = GetTimeWeightedAveragePrice(input);

            _sumVolume += volume;
            _sumPriceVolume += price * volume;

            if (_sumVolume == 0m)
            {
                // fallback to last price if no volume
                return new IndicatorResult(input.Value, IndicatorStatus.MathError);
            }

            return new IndicatorResult(_sumPriceVolume / _sumVolume, IndicatorStatus.Success);
        }

        /// <summary>
        /// Gets an estimated average price to use for the interval covered by the input trade bar.
        /// </summary>
        /// <param name="input">The current trade bar input</param>
        /// <returns>An estimated average price over the trade bar's interval</returns>
        protected virtual decimal GetTimeWeightedAveragePrice(TradeBar input)
        {
            return (input.Open + input.High + input.Low + input.Value) / 4;
        }

        /// <summary>
        /// Not used because we override ValidateAndComputeNextValue.
        /// </summary>
        protected override decimal ComputeNextValue(TradeBar input)
        {
            throw new NotImplementedException(
                $"{nameof(AnchoredVolumeWeightedAveragePrice)}.{nameof(ComputeNextValue)} should never be invoked.");
        }
        
    }
}