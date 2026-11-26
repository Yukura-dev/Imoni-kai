using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Imoni.Core
{
    public enum InviteJoinResult { Success, Empty, InProgress, NotFound }
    public enum LoadingStepState { Pending, Active, Done }

    /// <summary>Temporary local lobby/loading fixture timing. Replaced by actual world generation at M3.</summary>
    public sealed class LobbyFlowConfig
    {
        public int TeamSize { get; }
        public string SuccessCode { get; }
        public string BusyCode { get; }
        public int CodeMaxLength { get; }
        public IReadOnlyList<decimal> BotJoinSeconds { get; }
        public decimal WaitingDotSeconds { get; }
        public int WaitingDotCount { get; }
        public decimal LoadingSeconds { get; }
        public IReadOnlyList<int> StepEnds { get; }
        public IReadOnlyList<int> SyncThresholds { get; }
        public decimal TipSeconds { get; }
        private LobbyFlowConfig(JObject data)
        {
            TeamSize = data.SelectToken("team.size")?.Value<int>() ?? 0;
            JToken lobby = data.SelectToken("outGame.lobby") ?? throw new ArgumentException("outGame.lobby is required.");
            JToken loading = data.SelectToken("outGame.loading") ?? throw new ArgumentException("outGame.loading is required.");
            SuccessCode = lobby["successCode"]?.Value<string>(); BusyCode = lobby["busyCode"]?.Value<string>();
            CodeMaxLength = lobby["codeMaxLength"]?.Value<int>() ?? 0;
            BotJoinSeconds = Array.AsReadOnly(lobby["botJoinSeconds"].Values<decimal>().ToArray());
            WaitingDotSeconds = lobby["waitingDotSeconds"].Value<decimal>();
            WaitingDotCount = lobby["waitingDotCount"].Value<int>();
            LoadingSeconds = loading["durationSeconds"].Value<decimal>();
            StepEnds = Array.AsReadOnly(loading["stepEnds"].Values<int>().ToArray());
            SyncThresholds = Array.AsReadOnly(loading["syncThresholds"].Values<int>().ToArray());
            TipSeconds = loading["tipSeconds"].Value<decimal>();
            if (TeamSize <= 0 || CodeMaxLength <= 0 || string.IsNullOrWhiteSpace(SuccessCode) || string.IsNullOrWhiteSpace(BusyCode)
                || SuccessCode == BusyCode || SuccessCode.Length > CodeMaxLength || BusyCode.Length > CodeMaxLength
                || BotJoinSeconds.Count != TeamSize - 1 || BotJoinSeconds.Any(value => value < 0)
                || !BotJoinSeconds.SequenceEqual(BotJoinSeconds.OrderBy(value => value))
                || WaitingDotSeconds <= 0 || WaitingDotCount <= 0 || LoadingSeconds <= 0 || TipSeconds <= 0
                || StepEnds.Count == 0 || StepEnds.Last() != 100 || StepEnds.Any(value => value <= 0 || value > 100)
                || StepEnds.Distinct().Count() != StepEnds.Count || !StepEnds.SequenceEqual(StepEnds.OrderBy(value => value))
                || SyncThresholds.Count != TeamSize || SyncThresholds.Any(value => value < 0 || value > 100)
                || !SyncThresholds.SequenceEqual(SyncThresholds.OrderBy(value => value)))
                throw new ArgumentException("Invalid out-game lobby/loading configuration.");
        }
        public static LobbyFlowConfig FromJson(string json) => new LobbyFlowConfig(JObject.Parse(json));
    }

    public sealed class WaitingPresentation
    {
        public int MemberCount { get; }
        public int Remaining { get; }
        public bool Full => Remaining == 0;
        public int DotCount { get; }
        internal WaitingPresentation(int count, int remaining, int dots) { MemberCount = count; Remaining = remaining; DotCount = dots; }
    }
    public sealed class LoadingPresentation
    {
        public int Percent { get; }
        public decimal Progress => Percent / 100m;
        public bool Ready => Percent == 100;
        public IReadOnlyList<LoadingStepState> Steps { get; }
        public int SyncedMembers { get; }
        public int TipIndex { get; }
        internal LoadingPresentation(int percent, LoadingStepState[] steps, int synced, int tip)
        { Percent = percent; Steps = Array.AsReadOnly(steps); SyncedMembers = synced; TipIndex = tip; }
    }

    public static class LobbyPresentation
    {
        public static string ModeDescription(GameConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return "どちらもランキング対象(部門は別)。通常モード" + (config.DurationSeconds / 60).ToString("G29", CultureInfo.InvariantCulture)
                + "分×" + config.RoundsToClear + "ラウンド、エンドレスは通常クリアで解放";
        }
        public static string BonusHint(GameConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            BonusTier extra = config.BonusTiers.Skip(1).FirstOrDefault();
            string hint = "ボーナス素材は開始" + (config.BonusRevealSeconds / 60).ToString("G29", CultureInfo.InvariantCulture) + "分後に発表。";
            return extra == null ? hint + "目標達成で報酬アップ" : hint + extra.MinPercent + "%以上でさらに報酬アップ";
        }
        public static string NormalizeCode(string code) => (code ?? string.Empty).Trim().ToUpperInvariant();
        public static InviteJoinResult Join(LobbyFlowConfig config, string code)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            string normalized = NormalizeCode(code);
            if (normalized.Length == 0) return InviteJoinResult.Empty;
            if (normalized == config.BusyCode) return InviteJoinResult.InProgress;
            return normalized == config.SuccessCode ? InviteJoinResult.Success : InviteJoinResult.NotFound;
        }
        public static WaitingPresentation Waiting(LobbyFlowConfig config, decimal elapsedSeconds)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            int members = 1 + config.BotJoinSeconds.Count(time => elapsedSeconds >= time);
            int dots = (int)(decimal.Floor(elapsedSeconds / config.WaitingDotSeconds) % config.WaitingDotCount) + 1;
            return new WaitingPresentation(members, config.TeamSize - members, dots);
        }
        public static LoadingPresentation Loading(LobbyFlowConfig config, decimal elapsedSeconds, int tipCount)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (tipCount <= 0) throw new ArgumentOutOfRangeException(nameof(tipCount));
            int percent = (int)decimal.Floor(Math.Min(1, elapsedSeconds / config.LoadingSeconds) * 100);
            int start = 0;
            var states = new LoadingStepState[config.StepEnds.Count];
            for (int index = 0; index < states.Length; index++)
            {
                int end = config.StepEnds[index];
                states[index] = percent >= end ? LoadingStepState.Done : percent >= start ? LoadingStepState.Active : LoadingStepState.Pending;
                start = end;
            }
            return new LoadingPresentation(percent, states, config.SyncThresholds.Count(threshold => percent >= threshold),
                (int)(decimal.Floor(elapsedSeconds / config.TipSeconds) % tipCount));
        }
    }
}
