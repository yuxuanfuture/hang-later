using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HangLater.Windows.Models;

namespace HangLater.Windows.Services;

// Mirrors HangLaterCore/CompletionArchive.swift: group by the local completion
// date, newest group first and newest completion first within each group.
public sealed record CompletionSection(DateTime Start, IReadOnlyList<HangingTask> Tasks);

public static class CompletionArchive
{
    public static IReadOnlyList<CompletionSection> Sections(IEnumerable<HangingTask> tasks, CompletionGrouping grouping,
        DayOfWeek? firstDayOfWeek = null)
    {
        DayOfWeek firstDay = firstDayOfWeek ?? CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        DateTime Key(HangingTask task)
        {
            DateTime date = task.completedAt!.Value.LocalDateTime.Date;
            return grouping switch
            {
                CompletionGrouping.Month => new DateTime(date.Year, date.Month, 1),
                CompletionGrouping.Week => date.AddDays(-((7 + (int)date.DayOfWeek - (int)firstDay) % 7)),
                _ => date
            };
        }
        return tasks.Where(task => task.IsCompleted).GroupBy(Key).OrderByDescending(group => group.Key)
            .Select(group => new CompletionSection(group.Key, group.OrderByDescending(task => task.completedAt).ToArray())).ToArray();
    }

    public static string Title(DateTime date, CompletionGrouping grouping, bool chinese)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(chinese ? "zh-CN" : "en-US");
        string ShortDate(DateTime value) => value.ToString(chinese ? "M月d日" : "MMM d", culture);
        return grouping switch
        {
            CompletionGrouping.Month => date.ToString(chinese ? "yyyy年M月" : "MMMM yyyy", culture),
            CompletionGrouping.Week => ShortDate(date) + " – " + ShortDate(date.AddDays(6)),
            _ => date.ToString(chinese ? "M月d日 dddd" : "dddd, MMMM d", culture)
        };
    }
}
