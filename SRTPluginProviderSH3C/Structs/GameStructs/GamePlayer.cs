namespace SRTPluginProviderSH3C.Structs.GameStructs
{
    // SH3 PC player data.
    // HP is stored as a float at the player HP address.
    // Thresholds are approximate — adjust after testing.
    public struct GamePlayer
    {
        private float currentHP;

        public GamePlayer(float hp)
        {
            currentHP = hp;
        }

        public float CurrentHP => currentHP;
        public bool IsAlive     => CurrentHP > 0f;

        public PlayerStatus HealthState =>
            !IsAlive         ? PlayerStatus.Dead    :
            CurrentHP > 75f  ? PlayerStatus.Fine    :
            CurrentHP > 35f  ? PlayerStatus.Caution :
                               PlayerStatus.Danger;

        public string CurrentHealthState => HealthState.ToString();
    }

    public enum PlayerStatus
    {
        Dead,
        Fine,
        Caution,
        Danger,
    }
}
