using Godot;
using System;

[Tool]
public partial class Spinner : Sprite2D
{
	[Export]
	public float Speed = 10;

	public override void _Process(double delta)
	{
		Rotation += (float)(delta * Speed);
	}
}
