using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public partial class Modding : ColorRect
{
	public Instance Instance;

	[Export]
	Control NoLoaderWarning;

	[Export]
	Control Spinner, InstallFabricButton;
	[Export]
	Label InstallerText;
	[Export]
	ItemList SearchList;
	[Export]
	Texture2D PlaceholderModIcon;

	public override void _Ready()
	{
		NoLoaderWarning.Visible = Instance.Fabric == null;
	}

	async void InstallFabric()
	{
		InstallFabricButton.Visible = false;
		InstallerText.Text = "Getting loader";
		Spinner.Visible = true;

		var ls = await FabricMeta.GetLoaders(Instance.Id);
		var l = ls.FirstOrDefault(l => l.Loader.Stable);

		if(l == null)
		{
			InstallerText.Text = "Fabric is not available for this version";
			Spinner.Visible = false;
			return;
		}

		Directory.CreateDirectory(Instance.GetFabricDirectory());

		var fi = new InstanceFabricInfo()
		{
			Version = l.Loader.Version,
			LauncherMeta = l.LauncherMeta
		};


		async Task DownloadLib(FabricMetaLibrary l)
		{
			fi.Libraries.Add(l.Name);

			var outfile = Instance.GetFabricDirectory() + "/" + l.Name.Replace(':', '-').Replace(".", "-") + ".jar";

			if (File.Exists(outfile)) return;

			GD.Print(l.Name);
			var s = await new RequestBuilder(l.GetDownloadUrl()).Get<Stream>();
			var f = File.OpenWrite(outfile);
			await s.CopyToAsync(f);
			f.Close();
		}

		InstallerText.Text = "Downloading loader";
		// Download Loader
		{
			await DownloadLib(new FabricMetaLibrary() { Name = l.Loader.Maven, Url = FabricMeta.MavenUrl });
			await DownloadLib(new FabricMetaLibrary() { Name = l.Intermediary.Maven, Url = FabricMeta.MavenUrl });
		}

		InstallerText.Text = "Downloading libraries";
		// Download libraries
		{
			foreach (var lib in l.LauncherMeta.Libraries.Client)
				await DownloadLib(lib);

			foreach (var lib in l.LauncherMeta.Libraries.Common)
				await DownloadLib(lib);
		}

		Instance.Fabric = fi;
		await Instance.Save();

		NoLoaderWarning.Visible = false;
	}

	bool searching = false;
	async void SubmitSearch(string query)
	{
		if (searching) return;
		searching = true;

		SearchList.Clear();

		var s = await Modrinth.Search(query, new (string, string)[] { ("versions", Instance.Meta.Id), ("categories", "fabric") }, limit: 50);

		foreach (var m in s.Hits)
		{
			var i = SearchList.AddItem($"{m.Title} ({m.Downloads} ↓)");
			SearchList.SetItemIcon(i, PlaceholderModIcon);

			async void DownloadIcon()
			{
				var ext = m.IconUrl.Split('.').Last().ToLower();

				var imgstream = await new RequestBuilder(m.IconUrl).Get<Stream>();
				var imgms = new MemoryStream();
				await imgstream.CopyToAsync(imgms);

				var img = new Image();
				switch (ext)
				{
					case "jpg":
					case "jpeg":
						// Apparently JPEG decoding is broken?
						return;
						img.LoadJpgFromBuffer(imgms.ToArray());
						break;
					case "png":
						img.LoadPngFromBuffer(imgms.ToArray());
						break;
					default:
					case "svg":
						return;
				}

				if (IsInstanceValid(img))
				{
					var tex = ImageTexture.CreateFromImage(img);
					SearchList.SetItemIcon(i, tex);
				}
			}

			DownloadIcon();
		}
	
		searching = false;
	}
}
