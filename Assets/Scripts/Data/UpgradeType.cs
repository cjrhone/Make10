/// <summary>
/// Categories of upgrades available in the shop.
/// </summary>
public enum UpgradeType
{
    EnhancedNumber,     // Bonus BP for specific numbers in matches
    Multiplier,         // Affects multiplier mechanics
    Time,               // Affects time/duration
    TileWeight,         // Affects spawn probabilities
    Combo,              // Chain/combo bonuses
    RiskReward,         // High risk, high reward effects
    Information,        // QoL and visibility improvements
    Defensive,          // Recovery and safety nets
    BossFight,          // Boss-specific upgrades
    Special             // Unique mechanics (Free Space, etc.)
}

/// <summary>
/// Categories of snacks (passive artifacts).
/// </summary>
public enum SnackType
{
    Stopwatch,          // Emergency time when timer hits 0
    TenTenSandwich,     // Bonus BP every 10th Make10
    EnergyDrink,        // Start in Hot Streak mode
    StudyGlasses,       // Extended Hot Streak duration
    CoffeeMug,          // Starting multiplier bonus
    BrainFood,          // Base score increase
    Flashcards,         // Faster hints
    Calculator,         // Chance for double points
    Textbook,           // Percentage BP boost
    AlarmClock,         // Warning sound at low time
    StickyNotes,        // Enhanced number bonus increase
    RedBull,            // Higher Hot Streak multiplier
    CheatSheet,         // Start with hint showing
    ProteinBar,         // BP on multiplier increase
    LuckyCharm,         // Chance for wildcard spawns
    Metronome,          // Slower multiplier drain
    Tutor               // Reduced failure penalty
}

/// <summary>
/// Categories of rare artifacts (boss rewards).
/// </summary>
public enum ArtifactType
{
    GoldenPencil,       // Double Enhanced Number bonuses
    InfiniteEraser,     // Free grid reset per round
    TeachersPet,        // Shop discount
    Overachiever,       // Threshold completion bonus
    NightOwl,           // More time, lower multiplier cap
    Cramming,           // Earlier Hot Streak trigger
    Notebook            // Combo tracking (cosmetic)
}
