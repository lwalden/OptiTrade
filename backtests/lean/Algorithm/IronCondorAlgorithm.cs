// IronCondorAlgorithm.cs — Sprint 1.0 [S1C-003]
//
// Phase 1 LEAN backtest stub for the OptiMind iron condor strategy.
// All strategy parameters are sourced from StrategyConstants.cs (auto-generated).
//
// STATUS: Scaffold only. Real options logic is implemented in Sprint 1.0 execution phase.
//
// To run:
//   lean backtest --project backtests/lean --output backtests/lean/results
//
// Requires: .NET SDK 6+ and QuantConnect LEAN CLI installed.

using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using OptiMind.Backtests;

namespace OptiMind.Backtests.Algorithm
{
    /// <summary>
    /// Iron condor baseline backtest algorithm.
    /// Entry/exit parameters are defined in StrategyConstants (generated from strategies.yaml).
    /// </summary>
    public class IronCondorAlgorithm : QCAlgorithm
    {
        public override void Initialize()
        {
            // Backtest date range from config
            SetStartDate(2019, 1, 1);
            SetEndDate(2025, 12, 31);

            // Initial capital from config
            SetCash(StrategyConstants.BacktestInitialCapitalUsd);

            // TODO Sprint 1.0 execution: Add SPX/SPY options universe
            // AddIndexOption("SPX", Resolution.Minute);
            // AddOption("SPY", Resolution.Minute);

            // Log parameter_hash for reproducibility
            Log($"parameter_hash: {StrategyConstants.ParameterHash}");
            Log($"DTE range: {StrategyConstants.EntryDteMin}-{StrategyConstants.EntryDteMax} (target {StrategyConstants.EntryDteTarget})");
            Log($"Short delta target: {StrategyConstants.EntryShortDeltaTarget}");
            Log($"Profit target: {StrategyConstants.ExitProfitTargetPct:P0} | Stop: {StrategyConstants.ExitStopLossCreditMultiple}x credit");
        }

        public override void OnData(Slice data)
        {
            // TODO Sprint 1.0 execution: implement iron condor scan and entry logic
            // 1. Filter option chain by DTE and delta targets
            // 2. Check IVR filter (IvrMin / IvrMax)
            // 3. Construct 4-leg BAG order with SmartPricing
            // 4. Monitor positions for profit target, stop loss, and DTE checkpoints
        }
    }
}
