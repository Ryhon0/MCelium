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

		if (l == null)
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
				if(string.IsNullOrEmpty(m.IconUrl)) return;

				var ext = m.IconUrl.Split('.').Last().ToLower();

				var imgstream = await WebCache.Get(m.IconUrl);
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

	/*
	async Task DownloadMod(string query, string version, string loader)
			{
				var s = await Modrinth.Search(query, new (string, string)[] { ("versions", version), ("categories", loader) });

				var mod = s.Hits.First();

				var vers = await Modrinth.GetVersions(mod.ProjectID);
				var v = vers.First(v => v.GameVersions.Contains(version) && v.Loaders.Contains(loader));

				var f = v.Files.First(f => f.Primary);

				var modout = mcdir + "mods/" + f.Filename;
				if (!File.Exists(modout))
				{
					var ms = await new RequestBuilder(f.Url).Get<Stream>();
					var mf = File.OpenWrite(modout);
					await ms.CopyToAsync(mf);
					mf.Close();
				}

				foreach (var d in v.Dependencies.Where(d => d.DependencyType == "required"))
				{
					if (d.VersionId != null)
					{
						var dver = await Modrinth.GetVersion(d.VersionId);

						var df = dver.Files.First(f => f.Primary);

						var depout = mcdir + "mods/" + df.Filename;

						if (!File.Exists(depout))
						{
							var ds = await new RequestBuilder(f.Url).Get<Stream>();
							var dmf = File.OpenWrite(depout);
							await ds.CopyToAsync(dmf);
							dmf.Close();
						}
					}
					else
					{
						var dvers = await Modrinth.GetVersions(d.ProjectId);
						var dver = dvers.First(dv => dv.GameVersions.Contains(version) && dv.Loaders.Contains(loader));

						var df = dver.Files.First(f => f.Primary);

						var depout = mcdir + "mods/" + df.Filename;

						if (!File.Exists(depout))
						{
							var ds = await new RequestBuilder(f.Url).Get<Stream>();
							var dmf = File.OpenWrite(depout);
							await ds.CopyToAsync(dmf);
							dmf.Close();
						}
					}
				}
			}

			await DownloadMod("Sodium", "1.19.2", "fabric");
			await DownloadMod("Lithium", "1.19.2", "fabric");
			await DownloadMod("Iris Shaders", "1.19.2", "fabric");
			await DownloadMod("Mod Menu", "1.19.2", "fabric");
			await DownloadMod("Distant Horizons", "1.19.2", "fabric");
	*/
}
