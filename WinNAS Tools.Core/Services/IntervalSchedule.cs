namespace WinNASTools.Core.Services;

/// <summary>间隔天数 + 时刻 日程；错过则跳过到下一档。</summary>
public static class IntervalSchedule
{
    /// <summary>错过超过该时间则跳过，不补打。</summary>
    public static readonly TimeSpan MissWindow = TimeSpan.FromHours(2);

    public static DateTime ComputeInitialNextDue(int intervalDays, int hour, int minute, DateTime nowLocal)
    {
        intervalDays = Math.Max(1, intervalDays);
        hour = Math.Clamp(hour, 0, 23);
        minute = Math.Clamp(minute, 0, 59);

        var candidate = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, hour, minute, 0);
        if (candidate <= nowLocal)
            candidate = candidate.AddDays(1);
        // 每 N 天：首次也按间隔落点，避免「每 7 天」却在 24h 内就打。
        if (intervalDays > 1)
            candidate = candidate.AddDays(intervalDays - 1);
        return candidate;
    }

    public static DateTime AdvanceAfterSuccess(DateTime dueOrRunLocal, int intervalDays, int hour, int minute)
    {
        intervalDays = Math.Max(1, intervalDays);
        var day = dueOrRunLocal.Date.AddDays(intervalDays);
        return new DateTime(day.Year, day.Month, day.Day, Math.Clamp(hour, 0, 23), Math.Clamp(minute, 0, 59), 0);
    }

    /// <summary>
    /// 判断是否应打印；若已错过窗口则推进 NextDue（跳过），返回 false。
    /// </summary>
    public static bool TryEvaluate(
        ref DateTime nextDueLocal,
        DateTime nowLocal,
        int intervalDays,
        int hour,
        int minute,
        out bool skippedMissed)
    {
        skippedMissed = false;
        intervalDays = Math.Max(1, intervalDays);

        if (nowLocal < nextDueLocal)
            return false;

        if (nowLocal - nextDueLocal <= MissWindow)
            return true;

        // 错过：不断加间隔直到落到未来
        skippedMissed = true;
        while (nextDueLocal <= nowLocal)
            nextDueLocal = AdvanceAfterSuccess(nextDueLocal, intervalDays, hour, minute);

        return false;
    }

    public static string Describe(int intervalDays, int hour, int minute)
        => $"每 {Math.Max(1, intervalDays)} 天 {hour:D2}:{minute:D2}";
}
