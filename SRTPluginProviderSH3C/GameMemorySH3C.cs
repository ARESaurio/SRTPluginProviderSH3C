using SRTPluginProviderSH3C.Structs.GameStructs;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace SRTPluginProviderSH3C
{
    public struct GameMemorySH3C : IGameMemorySH3C
    {
        private const string IGT_TIMESPAN_STRING_FORMAT = @"hh\:mm\:ss";
        private const string BOSS_TIME_FORMAT = "{0:D2}:{1:D2}";

        public string GameName   => "SH3C";
        public string VersionInfo => FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;

        // ── Player ───────────────────────────────────────────────────────────
        public GamePlayer Player { get => _player; set => _player = value; }
        internal GamePlayer _player;

        // ── In-Game Time ─────────────────────────────────────────────────────
        public float IGT { get => _igt; }
        internal float _igt;

        // ── Beef Jerky ───────────────────────────────────────────────────────
        public short BeefJerky { get => _beefJerky; }
        internal short _beefJerky;

        // ── Weapons ──────────────────────────────────────────────────────────
        public short HandgunCount    { get => _handgunCount; }
        internal short _handgunCount;

        public short ShotgunCount    { get => _shotgunCount; }
        internal short _shotgunCount;

        public short MachineGunCount { get => _machineGunCount; }
        internal short _machineGunCount;

        // ── Run Stats ────────────────────────────────────────────────────────
        public byte  SaveCount     { get => _saveCount; }
        internal byte _saveCount;

        public short ItemCount     { get => _itemCount; }
        internal short _itemCount;

        public short ShootingKills { get => _shootingKills; }
        internal short _shootingKills;

        public short MeleeKills    { get => _meleeKills; }
        internal short _meleeKills;

        public float DamageTaken   { get => _damageTaken; }
        internal float _damageTaken;

        // ── Difficulty ───────────────────────────────────────────────────────
        public byte ActionDifficulty { get => _actionDifficulty; }
        internal byte _actionDifficulty;

        public byte RiddleDifficulty { get => _riddleDifficulty; }
        internal byte _riddleDifficulty;

        // ── Boss Fight Times ─────────────────────────────────────────────────
        public float WormTime       { get => _wormTime; }
        internal float _wormTime;

        public float MissionaryTime { get => _missionaryTime; }
        internal float _missionaryTime;

        public float LeonardTime    { get => _leonardTime; }
        internal float _leonardTime;

        public float AlessaTime     { get => _alessaTime; }
        internal float _alessaTime;

        public float GodTime        { get => _godTime; }
        internal float _godTime;

        // ── Calculated Properties ─────────────────────────────────────────────

        // IGT
        public TimeSpan IGTTimeSpan       => TimeSpan.FromSeconds(IGT);
        public string   IGTFormattedString => IGTTimeSpan.ToString(IGT_TIMESPAN_STRING_FORMAT, CultureInfo.InvariantCulture);

        // Difficulty strings
        public string ActionDifficultyString => ActionDifficulty switch
        {
            0 => "Beginner",
            1 => "Easy",
            2 => "Normal",
            3 => "Hard",
            _ => "Unknown"
        };

        public string RiddleDifficultyString => RiddleDifficulty switch
        {
            0 => "Easy",
            1 => "Normal",
            2 => "Hard",
            _ => "Unknown"
        };

        // Boss time formatted strings (MM:SS)
        public string WormTimeFormatted       => FormatBossTime(WormTime);
        public string MissionaryTimeFormatted => FormatBossTime(MissionaryTime);
        public string LeonardTimeFormatted    => FormatBossTime(LeonardTime);
        public string AlessaTimeFormatted     => FormatBossTime(AlessaTime);
        public string GodTimeFormatted        => FormatBossTime(GodTime);

        private static string FormatBossTime(float seconds)
        {
            TimeSpan ts = TimeSpan.FromSeconds(seconds);
            return string.Format(BOSS_TIME_FORMAT, (int)ts.TotalMinutes, ts.Seconds);
        }
    }
}
