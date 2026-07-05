namespace Shared;

public static class AlarmEvaluator
{
    public static AlarmPriority Evaluate(double value, AlarmThresholdsDto thresholds)
    {
        if (value <= thresholds.Priority3Low || value >= thresholds.Priority3High)
        {
            return AlarmPriority.Priority3;
        }

        if (value <= thresholds.Priority2Low || value >= thresholds.Priority2High)
        {
            return AlarmPriority.Priority2;
        }

        if (value <= thresholds.Priority1Low || value >= thresholds.Priority1High)
        {
            return AlarmPriority.Priority1;
        }

        return AlarmPriority.None;
    }

    public static ConsoleColor ToConsoleColor(AlarmPriority priority) =>
        priority switch
        {
            AlarmPriority.Priority1 => ConsoleColor.Yellow,
            AlarmPriority.Priority2 => ConsoleColor.DarkYellow,
            AlarmPriority.Priority3 => ConsoleColor.Red,
            _ => ConsoleColor.White
        };
}
