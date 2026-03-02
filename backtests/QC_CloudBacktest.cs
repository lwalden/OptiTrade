// QC_CloudBacktest.cs — QuantConnect Web IDE version
//
// Paste this entire file into a new C# algorithm on quantconnect.com.
// StrategyConstants is inlined here since the web IDE is single-file.
//
// After the backtest completes:
//   1. Click "Results" → note the key metrics
//   2. Download the full log (Logs tab → copy all)
//   3. Save log as backtests/lean/results/qc_cloud_log.txt
//   4. Run: uv run python scripts/parse_lean_results.py --log backtests/lean/results/qc_cloud_log.txt
//   5. Run: uv run python scripts/evaluate_phase1_gate.py

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

namespace QuantConnect.Algorithm.CSharp
{
    // ── Inlined strategy constants (source: optimind/config/strategies.yaml) ──
    // parameter_hash: fe43459060b2bf2ac80a339fa47dd8edaed08b520446d9faa24c810fd244f557
    public static class SC
    {
        public const int    EntryDteMin                  = 30;
        public const int    EntryDteTarget               = 45;
        public const int    EntryDteMax                  = 60;
        public const double EntryShortDeltaTarget        = 0.16;
        public const double EntryShortDeltaTolerance     = 0.04;
        public const int    EntryWingWidthSpy            = 5;
        public const double EntryMinCreditToWidthRatio   = 0.15;
        public const double ExitProfitTargetPct          = 0.50;
        public const double ExitStopLossCreditMultiple   = 2.0;
        public const int    ExitDteMandatoryClose        = 7;
        public const int    SizingDefaultContracts       = 1;
        public const double SlippagePerLegUsd            = 0.05;
        public const double CommissionPerContractUsd     = 0.65;
        public const int    InitialCapitalUsd            = 400000;
        public const string DateRangeStart               = "2019-01-01";
        public const string DateRangeEnd                 = "2025-12-31";
        public const string InSampleEnd                  = "2022-12-31";
        public const string OosStart                     = "2023-01-01";
        public const string ParameterHash               = "fe43459060b2bf2ac80a339fa47dd8edaed08b520446d9faa24c810fd244f557";
    }

    public class IronCondorAlgorithm : QCAlgorithm
    {
        private Symbol _spyOption;

        private IronCondorPosition _openPosition = null;

        private int    _totalTrades   = 0;
        private int    _winningTrades = 0;
        private double _grossProfit   = 0;
        private double _grossLoss     = 0;
        private double _totalSlippage = 0;
        private double _peakPortfolio = 0;
        private double _maxDrawdown   = 0;

        private DateTime _isEnd;
        private double   _portfolioAtIsEnd  = 0;
        private bool     _isEndRecorded     = false;
        private double   _oosStartEquity    = 0;
        private bool     _oosStartRecorded  = false;

        private DateTime _lastScanDate  = DateTime.MinValue;
        private bool     _diagLogged    = false;
        private DateTime _lastNoChainWarn = DateTime.MinValue;

        public override void Initialize()
        {
            var start = DateTime.Parse(SC.DateRangeStart);
            var end   = DateTime.Parse(SC.DateRangeEnd);
            SetStartDate(start.Year, start.Month, start.Day);
            SetEndDate(end.Year, end.Month, end.Day);
            SetCash(SC.InitialCapitalUsd);

            _isEnd = DateTime.Parse(SC.InSampleEnd);

            AddEquity("SPY", Resolution.Minute);

            // Use Minute resolution for options — QC daily options data has gaps pre-2021.
            // OnData still only acts on Wednesdays so frequency is the same; data coverage is better.
            var option = AddOption("SPY", Resolution.Minute);
            option.SetFilter(u => u
                .Expiration(TimeSpan.FromDays(SC.EntryDteMin), TimeSpan.FromDays(SC.EntryDteMax))
                .Strikes(-25, 25));
            _spyOption = option.Symbol;

            SetSecurityInitializer(s => {
                s.SetFeeModel(new ConstantFeeModel((decimal)SC.CommissionPerContractUsd));
                s.SetSlippageModel(new ConstantSlippageModel((decimal)SC.SlippagePerLegUsd));
            });

            Log($"parameter_hash: {SC.ParameterHash}");
            Log($"DTE range: {SC.EntryDteMin}-{SC.EntryDteMax} (target {SC.EntryDteTarget})");
            Log($"Delta target: {SC.EntryShortDeltaTarget} | Wing: {SC.EntryWingWidthSpy}pts | Profit: {SC.ExitProfitTargetPct:P0} | Stop: {SC.ExitStopLossCreditMultiple}x");
        }

        public override void OnData(Slice data)
        {
            TrackDrawdown();

            if (!_isEndRecorded && Time.Date >= _isEnd)
            {
                _portfolioAtIsEnd = (double)Portfolio.TotalPortfolioValue;
                _isEndRecorded    = true;
            }
            if (!_oosStartRecorded && Time.Date >= DateTime.Parse(SC.OosStart))
            {
                _oosStartEquity   = (double)Portfolio.TotalPortfolioValue;
                _oosStartRecorded = true;
            }

            if (_openPosition != null)
            {
                ManageOpenPosition();
                return;
            }

            // Scan any weekday — Wednesday preferred but fall through to Th/Fr if no chain that day.
            // Limit to one scan attempt per day.
            if (Time.DayOfWeek == DayOfWeek.Saturday || Time.DayOfWeek == DayOfWeek.Sunday)
                return;
            if (Time.Date <= _lastScanDate)
                return;

            _lastScanDate = Time.Date;  // mark scanned so we don't re-scan intra-day on minute bars

            if (!data.OptionChains.ContainsKey(_spyOption))
            {
                // Warn at most once per month to diagnose data gaps without flooding logs
                if ((Time.Date - _lastNoChainWarn).TotalDays >= 30)
                {
                    Log($"WARN: no option chain on {Time.Date:yyyy-MM-dd} ({Time.DayOfWeek})");
                    _lastNoChainWarn = Time.Date;
                }
                return;
            }
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

            // Log diagnostics once to confirm chain data quality
            if (!_diagLogged && calls.Count > 0)
            {
                var minDelta = calls.Min(c => Math.Abs((double)c.Greeks.Delta));
                var maxDelta = calls.Max(c => Math.Abs((double)c.Greeks.Delta));
                var minStrike = calls.Min(c => c.Strike);
                var maxStrike = calls.Max(c => c.Strike);
                Log($"DIAG: spot={spot:F2} contracts={contracts.Count} calls={calls.Count} deltaRange=[{minDelta:F3},{maxDelta:F3}] strikeRange=[{minStrike},{maxStrike}]");
                _diagLogged = true;
            }

            // Try delta-based selection; fall back to strike-based (~1 SD OTM) if Greeks are zero
            bool deltaAvailable = calls.Any(c => Math.Abs((double)c.Greeks.Delta) > 0.001);

            OptionContract shortCall, shortPut;
            if (deltaAvailable)
            {
                shortCall = SelectByDelta(calls, SC.EntryShortDeltaTarget, SC.EntryShortDeltaTolerance);
                shortPut  = SelectByDelta(puts,  SC.EntryShortDeltaTarget, SC.EntryShortDeltaTolerance);
            }
            else
            {
                // 1 SD ≈ spot * IV * sqrt(DTE/365); approximate with 7% OTM for 45 DTE
                // Select the OTM call/put strike closest to spot * 1.07 / spot * 0.93
                double callTarget = spot * 1.07;
                double putTarget  = spot * 0.93;
                shortCall = calls.OrderBy(c => Math.Abs((double)c.Strike - callTarget)).FirstOrDefault();
                shortPut  = puts.OrderBy(c => Math.Abs((double)c.Strike - putTarget)).FirstOrDefault();
                if (shortCall != null && shortPut != null)
                    Log($"DIAG: Using strike-based selection (no Greeks). callStrike={shortCall.Strike} putStrike={shortPut.Strike}");
            }

            if (shortCall == null) return;
            var longCall = calls.FirstOrDefault(c => c.Strike == shortCall.Strike + SC.EntryWingWidthSpy);
            if (longCall == null) return;

            if (shortPut == null) return;
            var longPut = puts.FirstOrDefault(c => c.Strike == shortPut.Strike - SC.EntryWingWidthSpy);
            if (longPut == null) return;

            if (shortPut.Strike >= (decimal)spot || shortCall.Strike <= (decimal)spot) return;

            double credit = Mid(shortCall) - Mid(longCall) + Mid(shortPut) - Mid(longPut);
            if (credit < SC.EntryWingWidthSpy * SC.EntryMinCreditToWidthRatio)
            {
                Log($"Entry rejected: credit={credit:F2} below min={SC.EntryWingWidthSpy * SC.EntryMinCreditToWidthRatio:F2}");
                return;
            }

            var legs = new List<Leg>
            {
                Leg.Create(shortCall.Symbol, -SC.SizingDefaultContracts),
                Leg.Create(longCall.Symbol,  +SC.SizingDefaultContracts),
                Leg.Create(shortPut.Symbol,  -SC.SizingDefaultContracts),
                Leg.Create(longPut.Symbol,   +SC.SizingDefaultContracts),
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

            _totalSlippage += SC.SlippagePerLegUsd * 4;

            int dte = (expiry.Value.Date - Time.Date).Days;
            Log($"ENTRY: {shortPut.Strike}P/{shortCall.Strike}C exp={expiry.Value:yyyy-MM-dd} credit={credit:F2} DTE={dte}");
        }

        private void ManageOpenPosition()
        {
            var pos = _openPosition;
            int dte = (pos.Expiry - Time.Date).Days;

            if (dte <= SC.ExitDteMandatoryClose)
            {
                ClosePosition("DTE<=7");
                return;
            }

            double val = EstimatePositionValue(pos);
            double pnl = pos.InitialCredit - val;

            if (pnl >= pos.InitialCredit * SC.ExitProfitTargetPct)
            {
                ClosePosition($"ProfitTarget pnl={pnl:F2}");
                return;
            }
            if (val >= pos.InitialCredit * SC.ExitStopLossCreditMultiple)
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
                Leg.Create(pos.ShortCall, +SC.SizingDefaultContracts),
                Leg.Create(pos.LongCall,  -SC.SizingDefaultContracts),
                Leg.Create(pos.ShortPut,  +SC.SizingDefaultContracts),
                Leg.Create(pos.LongPut,   -SC.SizingDefaultContracts),
            };

            ComboMarketOrder(legs, 1, asynchronous: false);
            _totalSlippage += SC.SlippagePerLegUsd * 4;

            double pnl = pos.InitialCredit - EstimatePositionValue(pos);
            if (pnl > 0) { _grossProfit += pnl; _winningTrades++; }
            else           _grossLoss   -= pnl;

            _totalTrades++;
            Log($"EXIT [{reason}] trade#{_totalTrades} exp={pos.Expiry:yyyy-MM-dd}");
            _openPosition = null;
        }

        public override void OnEndOfAlgorithm()
        {
            double final   = (double)Portfolio.TotalPortfolioValue;
            double initial = SC.InitialCapitalUsd;

            double years   = (EndDate - StartDate).TotalDays / 365.25;
            double cagr    = years > 0 ? Math.Pow(final / initial, 1.0 / years) - 1.0 : 0;

            double isYears = (_isEnd - StartDate).TotalDays / 365.25;
            double isCagr  = isYears > 0 && _portfolioAtIsEnd > 0
                ? Math.Pow(_portfolioAtIsEnd / initial, 1.0 / isYears) - 1.0 : 0;

            double oosYrs  = (EndDate - DateTime.Parse(SC.OosStart)).TotalDays / 365.25;
            double oosCagr = oosYrs > 0 && _oosStartEquity > 0
                ? Math.Pow(final / _oosStartEquity, 1.0 / oosYrs) - 1.0 : 0;

            double winRate      = _totalTrades > 0 ? (double)_winningTrades / _totalTrades : 0;
            double profitFactor = _grossLoss > 0 ? _grossProfit / _grossLoss : (_grossProfit > 0 ? 99 : 0);
            double slipDrag     = (_grossProfit + _grossLoss) > 0 ? _totalSlippage / (_grossProfit + _grossLoss) : 0;

            Log("=== Phase 1 Backtest Summary ===");
            Log($"parameter_hash:     {SC.ParameterHash}");
            Log($"date_range_start:   {SC.DateRangeStart}");
            Log($"date_range_end:     {SC.DateRangeEnd}");
            Log($"cagr_net:           {cagr:P2}");
            Log($"in_sample_cagr:     {isCagr:P2}");
            Log($"oos_cagr:           {oosCagr:P2}");
            Log($"max_drawdown:       {_maxDrawdown:P2}");
            Log($"win_rate:           {winRate:P2}");
            Log($"profit_factor:      {profitFactor:F2}");
            Log($"slippage_drag_pct:  {slipDrag:P2}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private DateTime? SelectExpiry(OptionChain chain)
        {
            var today  = Time.Date;
            var target = today.AddDays(SC.EntryDteTarget);
            return chain
                .Select(c => c.Expiry.Date)
                .Where(e => { int d = (e - today).Days; return d >= SC.EntryDteMin && d <= SC.EntryDteMax; })
                .Distinct()
                .OrderBy(e => Math.Abs((e - target).TotalDays))
                .Cast<DateTime?>()
                .FirstOrDefault();
        }

        private OptionContract SelectByDelta(List<OptionContract> contracts, double target, double tolerance)
        {
            // Try tight tolerance first, then widen to 0.10 as fallback
            var result = contracts
                .Where(c => Math.Abs(Math.Abs((double)c.Greeks.Delta) - target) <= tolerance)
                .OrderBy(c => Math.Abs(Math.Abs((double)c.Greeks.Delta) - target))
                .FirstOrDefault();
            if (result != null) return result;
            return contracts
                .Where(c => Math.Abs(Math.Abs((double)c.Greeks.Delta) - target) <= 0.10)
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

        private double Mid(OptionContract c)
        {
            if (c.BidPrice > 0 && c.AskPrice > 0)
                return (double)(c.BidPrice + c.AskPrice) / 2.0;
            return (double)c.LastPrice;  // fallback when bid/ask not populated
        }
        private double MidSec(Security s)
        {
            if (s.BidPrice > 0 && s.AskPrice > 0)
                return (double)(s.BidPrice + s.AskPrice) / 2.0;
            return (double)s.Price;
        }

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
