using SRTPluginProviderSH3C.Structs.GameStructs;
using System;

namespace SRTPluginProviderSH3C
{
    public interface IGameMemorySH3PS2
    {
        string GameName    { get; }
        string VersionInfo { get; }

        // Player
        GamePlayer Player { get; }

        // In-Game Time
        float    IGT                { get; }
        TimeSpan IGTTimeSpan        { get; }
        string   IGTFormattedString { get; }

        // Weapons — clip (equipped) + reserve
        short PistolClip      { get; }
        short PistolReserve   { get; }
        short ShotgunClip     { get; }
        short ShotgunReserve  { get; }
        short SMGClip         { get; }
        short SMGReserve      { get; }

        // Items
        short BeefJerky { get; }

        // Run Stats
        byte  SaveCount     { get; }
        short ItemCount     { get; }
        short ShootingKills { get; }
        short MeleeKills    { get; }
        short DamageTaken   { get; }

        // Difficulty
        byte   ActionDifficulty       { get; }
        byte   RiddleDifficulty       { get; }
        string ActionDifficultyString { get; }
        string RiddleDifficultyString { get; }

        // Game Area
        byte   GameArea       { get; }
        string GameAreaString { get; }

        // Boss Fight Times (seconds as float)
        float  WormTime                { get; }
        string WormTimeFormatted       { get; }
        float  MissionaryTime          { get; }
        string MissionaryTimeFormatted { get; }
        float  LeonardTime             { get; }
        string LeonardTimeFormatted    { get; }
        float  AlessaTime              { get; }
        string AlessaTimeFormatted     { get; }
        float  GodTime                 { get; }
        string GodTimeFormatted        { get; }
    }
}
