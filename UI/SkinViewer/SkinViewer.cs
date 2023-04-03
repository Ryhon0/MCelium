using Godot;
using System;
using System.IO;

public partial class SkinViewer : SubViewportContainer
{
	[Export]
	Node3D CameraArm;
	[Export]
	public Node3D Model;

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion m)
		{
			CameraArm.Rotation += new Vector3(0, m.Relative.X / 50f, 0);
		}
	}

	public async void ShowSkin(MinecraftSkin skin)
	{
		if (skin == null) return;
		
		var tex = (await new RequestBuilder(skin.Url)
			.Header("Accept", "image/png")
			.Get<Stream>())
			.LoadTexture("png");
		
		ShowSkin(skin, tex);
	}

	public void ShowSkin(MinecraftSkin skin, Texture2D tex)
	{
		if (skin == null) return;

		if (skin.Variant == MinecraftSkin.VariantSlim)
		{
			Model.GetNode<Node3D>("ClassicOverlay").Visible = false;
			Model.GetNode<Node3D>("Classic").Visible = false;
		}
		else
		{
			Model.GetNode<Node3D>("SlimOverlay").Visible = false;
			Model.GetNode<Node3D>("Slim").Visible = false;
		}

		(Model.GetChild<MeshInstance3D>(0)
			.Mesh.SurfaceGetMaterial(0) as StandardMaterial3D)
			.AlbedoTexture = tex;
	}
}
