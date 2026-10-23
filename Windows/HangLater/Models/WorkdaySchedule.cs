using System;

namespace HangLater.Windows.Models;

public readonly record struct WorkdaySchedule(int StartMinute, int EndMinute, bool EndsFollowingDay)
{
	public (DateTimeOffset Start, DateTimeOffset End)? IntervalContaining(DateTimeOffset moment)
	{
		DateTime date = moment.DateTime.Date;
		DateTime[] array = new DateTime[2]
		{
			date,
			date.AddDays(-1.0)
		};
		for (int i = 0; i < array.Length; i++)
		{
			DateTime dateTime = array[i];
			DateTimeOffset dateTimeOffset = new DateTimeOffset(dateTime.AddMinutes(Math.Clamp(StartMinute, 0, 1439)), moment.Offset);
			DateTimeOffset dateTimeOffset2 = new DateTimeOffset((EndsFollowingDay ? dateTime.AddDays(1.0) : dateTime).AddMinutes(Math.Clamp(EndMinute, 0, 1439)), moment.Offset);
			if (dateTimeOffset2 > dateTimeOffset && moment >= dateTimeOffset && moment < dateTimeOffset2)
			{
				return (dateTimeOffset, dateTimeOffset2);
			}
		}
		return null;
	}
}
