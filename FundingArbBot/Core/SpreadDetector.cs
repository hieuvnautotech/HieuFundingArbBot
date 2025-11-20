using HieuFundingArbBot.Models;
using FundingArbBot.Infra;
namespace HieuFundingArbBot.Core
{
    public class SpreadDetector
    {
        private readonly SimpleLogger _logger;
        private readonly double _threshold;

        public SpreadDetector(SimpleLogger logger, double threshold)
        {
            _logger = logger;
            _threshold = threshold;
        }

        // compute spread HL - Lighter
        public double ComputeSpread(FundingRate hl, FundingRate lighter)
        {
            if (hl == null || lighter == null) throw new ArgumentNullException();
            var spread = hl.Rate - lighter.Rate;
            _logger.Debug($"Computed spread = {spread:F6} (HL {hl.Rate:F6} - Lighter {lighter.Rate:F6})");
            return spread;
        }

        public bool IsCrossingThreshold(double spread)
        {
            return spread >= _threshold;
        }
    }
}