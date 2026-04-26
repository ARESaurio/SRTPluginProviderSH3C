using SRTPluginProviderSH3C.Structs.GameStructs;
using System;

namespace SRTPluginProviderSH3C
{
    public interface IGameMemorySH3C
    {
        string GameName    { get; }
        string VersionInfo { get; }

        // Player
        GamePlayer Player  { get; }

        // In-Game Time
        float    IGT               { get; }
        TimeSpan IGTTimeSpan       { get; }
        string   IGTFormattedString { get; }

        // Items
        short BeefJerky { get; }

        // Weapons
        short HandgunCount    { get; }
        short ShotgunCount    { get; }
        short MachineGunCount { get; }

        // Run Stats
        byte  SaveCount      { get; }
        short ItemCount      { get; }
        short ShootingKills  { get; }
        short MeleeKills     { get; }
        float DamageTaken    { get; }

        // Difficulty
        byte   ActionDifficulty       { get; }
        byte   RiddleDifficulty       { get; }
        string ActionDifficultyString { get; }
        string RiddleDifficultyString { get; }

        // Boss Fight Times (seconds)
        float  WormTime            { get; }
        string WormTimeFormatted   { get; }
        float  MissionaryTime          { get; }
        string MissionaryTimeFormatted { get; }
        float  LeonardTime            { get; }
        string LeonardTimeFormatted   { get; }
        float  AlessaTime             { get; }
        string AlessaTimeFormatted    { get; }
        float  GodTime                { get; }
        string GodTimeFormatted       { get; }
    }
}
