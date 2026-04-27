using SRTPluginProviderSH3C.Structs.GameStructs;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace SRTPluginProviderSH3C
{
    public struct GameMemorySH3PS2 : IGameMemorySH3PS2
    {
        private const string IGT_TIMESPAN_FORMAT = @"hh\:mm\:ss";
        private const string BOSS_TIME_FORMAT    = "{0:D2}:{1:D2}";

        public string GameName    => "SH3C";
        public string VersionInfo => FileVersionInfo.GetVersionInfo(
            Assembly.GetExecutingAssembly().Location).FileVersion;

        // ── Player ────────────────────────────────────────────────────────────
        public GamePlayer Player { get => _player; set => _player = value; }
        internal GamePlayer _player;

        // ── In-Game Time ──────────────────────────────────────────────────────
        public float IGT { get => _igt; }
        internal float _igt;

        // ── Weapons (clip + reserve) ──────────────────────────────────────────
        public short PistolClip     { get => _pistolClip; }
        internal short _pistolClip;

        public short PistolReserve  { get => _pistolReserve; }
        internal short _pistolReserve;

        public short ShotgunClip    { get => _shotgunClip; }
        internal short _shotgunClip;

        public short ShotgunReserve { get => _shotgunReserve; }
        internal short _shotgunReserve;

        public short SMGClip        { get => _smgClip; }
        internal short _smgClip;

        public short SMGReserve     { get => _smgReserve; }
        internal short _smgReserve;

        // ── Items ─────────────────────────────────────────────────────────────
        public short BeefJerky { get => _beefJerky; }
        internal short _beefJerky;

        // ── Run Stats ─────────────────────────────────────────────────────────
        public byte  SaveCount     { get => _saveCount; }
        internal byte _saveCount;

        public short ItemCount     { get => _itemCount; }
        internal short _itemCount;

        public short ShootingKills { get => _shootingKills; }
        internal short _shootingKills;

        public short MeleeKills    { get => _meleeKills; }
        internal short _meleeKills;

        public short DamageTaken   { get => _damageTaken; }
        internal short _damageTaken;

        // ── Difficulty ────────────────────────────────────────────────────────
        public byte ActionDifficulty { get => _actionDifficulty; }
        internal byte _actionDifficulty;

        public byte RiddleDifficulty { get => _riddleDifficulty; }
        internal byte _riddleDifficulty;

        // ── Game Area ─────────────────────────────────────────────────────────
        public byte GameArea { get => _gameArea; }
        internal byte _gameArea;

        // ── Boss Fight Times ──────────────────────────────────────────────────
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

        public TimeSpan IGTTimeSpan        => TimeSpan.FromSeconds(IGT);
        public string   IGTFormattedString => IGTTimeSpan.ToString(IGT_TIMESPAN_FORMAT, CultureInfo.InvariantCulture);

        public string ActionDifficultyString => ActionDifficulty switch
        {
            0 => "Beginner",
            1 => "Easy",
            2 => "Normal",
            3 => "Hard",
            4 => "Extreme 1",
            5 => "Extreme 2",
            _ => "Unknown"
        };

        public string RiddleDifficultyString => RiddleDifficulty switch
        {
            0 => "Easy",
            1 => "Normal",
            2 => "Hard",
            _ => "Unknown"
        };

        public string GameAreaString => GameArea switch
        {
            0x00 => "Title Screen",
            0x01 => "Mall 1F",
            0x02 => "Mall 2F",
            0x03 => "Mall OW 1F",
            0x04 => "Mall OW 2F",
            0x05 => "Mall OW 3F",
            0x06 => "Subway",
            0x07 => "Lower Subway",
            0x08 => "Train",
            0x09 => "Underpass",
            0x0A => "Sewers",
            0x0B => "Construction",
            0x0C => "Office 2F",
            0x0D => "Office 3F",
            0x0E => "Office Stairs",
            0x0F => "Office OW 1F",
            0x10 => "Office OW 2F",
            0x11 => "Office OW 4F",
            0x12 => "Office OW 5F",
            0x13 => "SH Streets",
            0x14 => "Apartment",
            0x15 => "Car Ride",
            0x16 => "Hotel Room",
            0x17 => "SH Streets 2",
            0x18 => "Heaven's Night",
            0x19 => "Hospital 1F",
            0x1A => "Hospital 2F",
            0x1B => "Hospital 3F",
            0x1C => "Hospital Roof",
            0x1D => "Tunnels",
            0x1E => "Hospital OW 1F",
            0x1F => "Hospital OW 2F",
            0x20 => "Hospital OW 3F",
            0x21 => "Amusement Park",
            0x22 => "Post-Coaster",
            0x23 => "Merry Go Round",
            0x24 => "Church Entrance",
            0x25 => "Church Back",
            0x26 => "Nowhere",
            0x27 => "God Dungeon",
            _    => $"Area 0x{GameArea:X2}"
        };

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
