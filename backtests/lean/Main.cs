// Main.cs — IronCondorAlgorithm [S1C-003, Sprint 1.0]
//
// Phase 1 LEAN backtest: SPY iron condor baseline.
// All parameters sourced from StrategyConstants.cs (auto-generated from strategies.yaml).
//
// Strategy:
//   - Sell SPY iron condors, DTE 30-60 (target 45), weekly scan on Wednesdays
//   - Short strikes at ~0.16 delta (~1 SD), wing width 5 points
//   - Exit at 50% profit OR 2x credit stop OR DTE <= 7
//   - 1 contract per position, max 1 open condor at a time (Phase 1 baseline)

using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Orders.Slippage;
using QuantConnect.Securities;
using QuantConnect.Securities.Option;
using OptiMind.Backtests;

namespace OptiMind.Backtests.Algorithm
{
    public class IronCondorAlgorithm : QCAlgorithm
    {
        private Symbol _spyOption;

        private IronCondorPosition _openPosition = null;

        private int    _totalTrades      = 0;
        private int    _winningTrades    = 0;
        private double _grossProfit      = 0;
        private double _grossLoss        = 0;
        private double _totalSlippage    = 0;
        private double _peakPortfolio    = 0;
        private double _maxDrawdown      = 0;

        private DateTime _isEnd;
        private double   _portfolioAtIsEnd = 0;
        private bool     _isEndRecorded    = false;
        private double   _oosStartEquity   = 0;
        private bool     _oosStartRecorded = false;

        private DateTime _lastScanDate = DateTime.MinValue;

        public override void Initialize()
        {
            var start = DateTime.Parse(StrategyConstants.BacktestDateRangeStart);
            var end   = DateTime.Parse(StrategyConstants.BacktestDateRangeEnd);
            SetStartDate(start.Year, start.Month, start.Day);
            SetEndDate(end.Year, end.Month, end.Day);
            SetCash(StrategyConstants.BacktestInitialCapitalUsd);

            _isEnd = DateTime.Parse(StrategyConstants.BacktestInSampleEnd);

            AddEquity("SPY", Resolution.Daily);

            var option = AddOption("SPY", Resolution.Daily);
            option.SetFilter(u => u
                .Expiration(TimeSpan.FromDays(StrategyConstants.EntryDteMin), TimeSpan.FromDays(StrategyConstants.EntryDteMax))
                .Strikes(-10, 10));
            _spyOption = option.Symbol;

            SetSecurityInitializer(s => {
                s.SetFeeModel(new ConstantFeeModel((decimal)StrategyConstants.BacktestCommissionPerContractUsd));
                s.SetSlippageModel(new ConstantSlippageModel((decimal)StrategyConstants.BacktestSlippagePerLegUsd));
            });

            Log($"IronCondorAlgorithm initialized | parameter_hash: {StrategyConstants.ParameterHash}");
            Log($"DTE {StrategyConstants.EntryDteMin}-{StrategyConstants.EntryDteMax} | delta {StrategyConstants.EntryShortDeltaTarget} | wing {StrategyConstants.EntryWingWidthSpy}pts");
            Log($"Profit target: {StrategyConstants.ExitProfitTargetPct:P0} | Stop: {StrategyConstants.ExitStopLossCreditMultiple}x credit | Mandatory close DTE<={StrategyConstants.ExitDteManagement_2_Dte}");
        }

        public override void OnData(Slice data)
        {
            TrackDrawdown();

            if (!_isEndRecorded && Time.Date >= _isEnd)
            {
                _portfolioAtIsEnd = (double)Portfolio.TotalPortfolioValue;
                _isEndRecorded    = true;
            }
            if (!_oosStartRecorded && Time.Date >= DateTime.Parse(StrategyConstants.BacktestOosStart))
            {
                _oosStartEquity   = (double)Portfolio.TotalPortfolioValue;
                _oosStartRecorded = true;
            }

            if (_openPosition != null)
            {
                ManageOpenPosition();
                return;
            }

            if (Time.DayOfWeek != DayOfWeek.Wednesday || Time.Date <= _lastScanDate)
                return;

            if (!data.OptionChains.ContainsKey(_spyOption))
                return;

            TryEnterCondor(data.OptionChains[_spyOption]);
        }

        private void TryEnterCondor(OptionChain chain)
        {
            var underlying = chain.Underlying?.Price;
            if (underlying == null || underlying <= 0) return;
            double spot = (double)underlying;

            var expiry = SelectExpiry(chain);
            if (expiry == null) return;

            var contracts = chain.Where(c => c.Expiry.Date == expiry.Value.Date).ToList();
            if (contracts.Count < 4) return;

            var calls = contracts.Where(c => c.Right == OptionRight.Call).OrderBy(c => c.Strike).ToList();
            var puts  = contracts.Where(c => c.Right == OptionRight.Put).OrderBy(c => c.Strike).ToList();

            var shortCall = SelectByDelta(calls, StrategyConstants.EntryShortDeltaTarget, StrategyConstants.EntryShortDeltaTolerance);
            if (shortCall == null) return;
            var longCall = calls.FirstOrDefault(c => c.Strike == shortCall.Strike + StrategyConstants.EntryWingWidthSpy);
            if (longCall == null) return;

            var shortPut = SelectByDelta(puts, StrategyConstants.EntryShortDeltaTarget, StrategyConstants.EntryShortDeltaTolerance);
            if (shortPut == null) return;
            var longPut = puts.FirstOrDefault(c => c.Strike == shortPut.Strike - StrategyConstants.EntryWingWidthSpy);
            if (longPut == null) return;

            if (shortPut.Strike >= (decimal)spot || shortCall.Strike <= (decimal)spot) return;

            double credit = Mid(shortCall) - Mid(longCall) + Mid(shortPut) - Mid(longPut);
            if (credit < StrategyConstants.EntryWingWidthSpy * StrategyConstants.EntryMinCreditToWidthRatio)
            {
                Log($"Entry rejected: credit={credit:F2} below min");
                return;
            }

            var legs = new List<Leg>
            {
                Leg.Create(shortCall.Symbol, -StrategyConstants.SizingDefaultContracts),
                Leg.Create(longCall.Symbol,  +StrategyConstants.SizingDefaultContracts),
                Leg.Create(shortPut.Symbol,  -StrategyConstants.SizingDefaultContracts),
                Leg.Create(longPut.Symbol,   +StrategyConstants.SizingDefaultContracts),
            };

            ComboMarketOrder(legs, 1, asynchronous: false);

            _openPosition = new IronCondorPosition
            {
                Expiry        = expiry.Value.Date,
                InitialCredit = credit,
                ShortCall     = shortCall.Symbol,
                LongCall      = longCall.Symbol,
                ShortPut      = shortPut.Symbol,
                LongPut       = longPut.Symbol,
            };

            _lastScanDate  = Time.Date;
            _totalSlippage += StrategyConstants.BacktestSlippagePerLegUsd * 4;

            int dte = (expiry.Value.Date - Time.Date).Days;
            Log($"ENTRY: {shortPut.Strike}P/{shortCall.Strike}C exp={expiry.Value:yyyy-MM-dd} credit={credit:F2} DTE={dte}");
        }

        private void ManageOpenPosition()
        {
            var pos = _openPosition;
            int dte = (pos.Expiry - Time.Date).Days;

            if (dte <= StrategyConstants.ExitDteManagement_2_Dte)
            {
                ClosePosition("DTE<=7");
                return;
            }

            double val = EstimatePositionValue(pos);
            double pnl = pos.InitialCredit - val;

            if (pnl >= pos.InitialCredit * StrategyConstants.ExitProfitTargetPct)
            {
                ClosePosition($"ProfitTarget pnl={pnl:F2}");
                return;
            }
            if (val >= pos.InitialCredit * StrategyConstants.ExitStopLossCreditMultiple)
            {
                ClosePosition($"StopLoss val={val:F2}");
                return;
            }
        }

        private void ClosePosition(string reason)
        {
            var pos = _openPosition;
            var legs = new List<Leg>
            {
                Leg.Create(pos.ShortCall, +StrategyConstants.SizingDefaultContracts),
                Leg.Create(pos.LongCall,  -StrategyConstants.SizingDefaultContracts),
                Leg.Create(pos.ShortPut,  +StrategyConstants.SizingDefaultContracts),
                Leg.Create(pos.LongPut,   -StrategyConstants.SizingDefaultContracts),
            };

            ComboMarketOrder(legs, 1, asynchronous: false);
            _totalSlippage += StrategyConstants.BacktestSlippagePerLegUsd * 4;

            double pnl = pos.InitialCredit - EstimatePositionValue(pos);
            if (pnl > 0) { _grossProfit += pnl; _winningTrades++; }
            else            _grossLoss   -= pnl;

            _totalTrades++;
            Log($"EXIT [{reason}] trade#{_totalTrades} exp={pos.Expiry:yyyy-MM-dd}");
            _openPosition = null;
        }

        public override void OnEndOfAlgorithm()
        {
            double final   = (double)Portfolio.TotalPortfolioValue;
            double initial = StrategyConstants.BacktestInitialCapitalUsd;

            double years   = (EndDate - StartDate).TotalDays / 365.25;
            double cagr    = years > 0 ? Math.Pow(final / initial, 1.0 / years) - 1.0 : 0;

            double isYears = (_isEnd - StartDate).TotalDays / 365.25;
            double isCagr  = isYears > 0 && _portfolioAtIsEnd > 0
                ? Math.Pow(_portfolioAtIsEnd / initial, 1.0 / isYears) - 1.0 : 0;

            double oosYrs  = (EndDate - DateTime.Parse(StrategyConstants.BacktestOosStart)).TotalDays / 365.25;
            double oosCagr = oosYrs > 0 && _oosStartEquity > 0
                ? Math.Pow(final / _oosStartEquity, 1.0 / oosYrs) - 1.0 : 0;

            double winRate      = _totalTrades > 0 ? (double)_winningTrades / _totalTrades : 0;
            double profitFactor = _grossLoss > 0 ? _grossProfit / _grossLoss : (_grossProfit > 0 ? 99 : 0);
            double slipDrag     = (_grossProfit + _grossLoss) > 0 ? _totalSlippage / (_grossProfit + _grossLoss) : 0;

            Log("=== Phase 1 Backtest Summary ===");
            Log($"parameter_hash:     {StrategyConstants.ParameterHash}");
            Log($"date_range_start:   {StrategyConstants.BacktestDateRangeStart}");
            Log($"date_range_end:     {StrategyConstants.BacktestDateRangeEnd}");
            Log($"cagr_net:           {cagr:P2}");
            Log($"in_sample_cagr:     {isCagr:P2}");
            Log($"oos_cagr:           {oosCagr:P2}");
            Log($"max_drawdown:       {_maxDrawdown:P2}");
            Log($"win_rate:           {winRate:P2}");
            Log($"profit_factor:      {profitFactor:F2}");
            Log($"slippage_drag_pct:  {slipDrag:P2}");
            Log($"total_trades:       {_totalTrades}");
            Log($"final_equity:       {final:C2}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private DateTime? SelectExpiry(OptionChain chain)
        {
            var today  = Time.Date;
            var target = today.AddDays(StrategyConstants.EntryDteTarget);
            return chain
                .Select(c => c.Expiry.Date)
                .Where(e => { int d = (e - today).Days; return d >= StrategyConstants.EntryDteMin && d <= StrategyConstants.EntryDteMax; })
                .Distinct()
                .OrderBy(e => Math.Abs((e - target).TotalDays))
                .Cast<DateTime?>()
                .FirstOrDefault();
        }

        private OptionContract SelectByDelta(List<OptionContract> contracts, double target, double tolerance)
        {
            return contracts
                .Where(c => Math.Abs(Math.Abs((double)c.Greeks.Delta) - target) <= tolerance)
                .OrderBy(c => Math.Abs(Math.Abs((double)c.Greeks.Delta) - target))
                .FirstOrDefault();
        }

        private double EstimatePositionValue(IronCondorPosition pos)
        {
            if (!Securities.ContainsKey(pos.ShortCall) || !Securities.ContainsKey(pos.LongCall) ||
                !Securities.ContainsKey(pos.ShortPut)  || !Securities.ContainsKey(pos.LongPut))
                return pos.InitialCredit;

            double sc = MidSec(Securities[pos.ShortCall]);
            double lc = MidSec(Securities[pos.LongCall]);
            double sp = MidSec(Securities[pos.ShortPut]);
            double lp = MidSec(Securities[pos.LongPut]);
            return sc - lc + sp - lp;
        }

        private double Mid(OptionContract c) => (double)(c.BidPrice + c.AskPrice) / 2.0;
        private double MidSec(Security s)    => (double)(s.BidPrice + s.AskPrice) / 2.0;

        private void TrackDrawdown()
        {
            double eq = (double)Portfolio.TotalPortfolioValue;
            if (eq > _peakPortfolio) _peakPortfolio = eq;
            if (_peakPortfolio > 0)
            {
                double dd = (_peakPortfolio - eq) / _peakPortfolio;
                if (dd > _maxDrawdown) _maxDrawdown = dd;
            }
        }
    }

    internal class IronCondorPosition
    {
        public DateTime Expiry        { get; set; }
        public double   InitialCredit { get; set; }
        public Symbol   ShortCall     { get; set; }
        public Symbol   LongCall      { get; set; }
        public Symbol   ShortPut      { get; set; }
        public Symbol   LongPut       { get; set; }
    }
}
