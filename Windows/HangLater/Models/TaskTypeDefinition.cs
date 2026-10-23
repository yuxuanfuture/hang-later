using System;

namespace HangLater.Windows.Models;

public sealed class TaskTypeDefinition
{
	public Guid id { get; set; } = Guid.NewGuid();

	public string name { get; set; } = "";

	public uint colorHex { get; set; }

	public TaskTypeDefinition Clone()
	{
		return new TaskTypeDefinition
		{
			id = id,
			name = name,
			colorHex = colorHex
		};
	}
}
