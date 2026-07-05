namespace Shared;

public enum DataQuality
{
    GOOD = 0,
    UNCERTAIN = 1,
    BAD = 2
}

public enum SensorStatus
{
    Standby = 0,
    Active = 1,
    InactiveBlocked = 2,
    DosBlocked = 3,
    Bad = 4
}

public enum SensorMode
{
    Normal = 0,
    Bad = 1,
    BadSignature = 2,
    Dos = 3,
    Blocked = 4
}

public enum AlarmPriority
{
    None = 0,
    Priority1 = 1,
    Priority2 = 2,
    Priority3 = 3
}
