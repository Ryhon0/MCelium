using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class Main
{
	[Export]
	SkinViewer SkinViewer;
	[Export]
	ItemList SkinList;

	List<(MinecraftSkin skin, ImageTexture tex)> Skins = new();

	async void SkinsMain()
	{
		var p = Profiles.First();
		SkinList.Clear();

		foreach (var s in p.MCProfile.Skins)
		{
			var ss = await WebCache.Get(s.Url);
			var tex = ss.LoadTexture("png");

			var i = SkinList.AddItem(s.Alias);
			SkinList.SetItemIcon(i, tex);

			Skins.Add((s, tex));
		}

		{
			var i = SkinList.AddItem("Reset Skin");
			SkinList.SetItemIcon(i, NewInstanceIcon);
		}
		{
			var i = SkinList.AddItem("Upload Skin");
			SkinList.SetItemIcon(i, NewInstanceIcon);
		}
	}

	void OnSkinSelected(int i)
	{
		if (i < 0 || i >= Skins.Count) return;

		var s = Skins[i];
		SkinViewer.ShowSkin(s.skin, s.tex);
	}

	async void OnSkinActivated(int i)
	{
		if (i < 0 || i >= Skins.Count)
		{
			if (i == Skins.Count)
			{
				GD.Print("Reset");
			}
			else if (i == Skins.Count + 1)
			{
				GD.Print("Upload");

				var fd = new FileDialog();
				fd.FileMode = FileDialog.FileModeEnum.OpenFile;
				fd.Access = FileDialog.AccessEnum.Filesystem;
				fd.Filters = new string[] {"*.png"};
				AddChild(fd);
				fd.Popup(new Rect2I(Vector2I.Zero, DisplayServer.WindowGetSize()));

				string variant = MinecraftSkin.VariantClassic;
				var vb = fd.AddButton("Variant: CLASSIC", true, "chgange_variant");

				fd.CustomAction += (StringName act) =>
				{
					if(act != "chgange_variant") return;
				
					var isSlim = variant == MinecraftSkin.VariantSlim;

					variant = isSlim ? MinecraftSkin.VariantClassic : MinecraftSkin.VariantSlim;

					vb.Text = "Variant: " + variant;
				};

				fd.FileSelected += async (string f) =>
				{
					GD.Print(f);
					var fs = System.IO.File.OpenRead(f);

					var p = Profiles.First();
					await Minecraft.UploadSkin(p.MCToken, variant, fs);
				};
			}

			return;
		};

		var s = Skins[i];
		SkinViewer.ShowSkin(s.skin, s.tex);
	}
}