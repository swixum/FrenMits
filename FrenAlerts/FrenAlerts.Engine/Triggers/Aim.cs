namespace FrenAlerts.Engine;

public enum Aim
{
    Anyone = 0,
    Me = 1,
    NotMe = 2,
    Enemy = 3,
    AnyPlayer = 4,
    OtherPlayer = 5,
    Untargeted = 6,
}

public static class ActorId
{
    public static bool IsPlayer(uint id) => (id >> 28) == 1;
}
