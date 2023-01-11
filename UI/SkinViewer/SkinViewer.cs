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
			CameraArm.Rotation += new Vector3(0, m.Relative.x / 50f, 0);
		}
	}

	public async void ShowSkin(MinecraftSkin skin)
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
		var pngstream = (await new RequestBuilder(skin.Url).Header("Accept", "image/png").Get<Stream>());
		var pngms = new MemoryStream();
		await pngstream.CopyToAsync(pngms);

		var img = new Image();
		img.LoadPngFromBuffer(pngms.ToArray());

		var tex = ImageTexture.CreateFromImage(img);
		(Model.GetChild<MeshInstance3D>(0)
			.Mesh.SurfaceGetMaterial(0) as StandardMaterial3D)
			.AlbedoTexture = tex;
	}
}
