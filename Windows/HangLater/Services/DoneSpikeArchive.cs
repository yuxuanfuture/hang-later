using System;
using System.Collections.Generic;
using System.Linq;
using HangLater.Windows.Models;

namespace HangLater.Windows.Services;

public static class DoneSpikeArchive
{
    public static IReadOnlyList<HangingTask> EligibleCompleted(IEnumerable<HangingTask> tasks,
        WorkdaySchedule schedule, DateTimeOffset now)
    {
        var interval = schedule.IntervalContaining(now);
        if (!interval.HasValue) return Array.Empty<HangingTask>();

        // Eligibility follows completion time, independently of when a slip was published.
        return tasks.Where(task => task.completedAt is { } completed
                && completed >= interval.Value.Start && completed < interval.Value.End)
            .OrderByDescending(task => task.completedAt)
            .ToArray();
    }
}
