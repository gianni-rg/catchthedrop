namespace Beps.CatchTheDrop
{
    public enum Level
    {
        LevelFull = 50,
        LevelHalf = 25,
        LevelChange = 50
    }

    public enum Hands
    {
        None = 0x00,
        Left = 0x01,
        Right = 0x02
    }

    public enum PolyType
    {
        None = 0x00,
        Circle = 0x40,
        Bubble = 0x80,
        All = 0x7f
    }

    public enum HitType
    {
        None = 0x00,
        Hand = 0x01,
        Captured = 0x02,
        Arm = 0x04
    }
}
