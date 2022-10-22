using Godot;
using System;

public partial class TimerProgressBar : Godot.Range
{
	[Export]
	Timer Timer;

	[Export]
	public int Precission = 10;

	public override void _Process(double delta)
	{
		MinValue = 0;
		MaxValue = Timer.WaitTime * Precission;
		Value = Timer.TimeLeft * Precission;
	}
}
