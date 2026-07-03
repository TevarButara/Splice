namespace Splice.Core
{
    // Two-side model covers every matching mode (1:1, 2:1, 4:1): teammates share a Team,
    // so N-vs-1 layouts fall out of team membership without per-player special casing.
    public enum Team
    {
        Invaders,
        Defenders
    }
}
