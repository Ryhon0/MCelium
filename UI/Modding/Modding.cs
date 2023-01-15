using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

public partial class Modding : ColorRect
{
	public Instance Instance;

	[Export]
	Control NoLoaderWarning;

	[Export]
	Control Spinner, InstallFabricButton;
	[Export]
	Label InstallerLabel, InstallerSubLabel;
	[Export]
	ItemList SearchList, InstalledList;
	[Export]
	Control InstalledPage;

	[Export]
	Texture2D PlaceholderModIcon;

	List<ModrinthProject> Hits;

	public override void _Ready()
	{
		NoLoaderWarning.Visible = true;

		if (Instance.Fabric != null)
			OnFabricInstalled();
	}

	void OnFabricInstalled()
	{
		NoLoaderWarning.Visible = false;

		InstalledPage.Name = $"Installed ({Instance.Fabric.Mods.Count})";
		InstalledList.Clear();
		foreach (var m in Instance.Fabric.Mods)
		{
			var title = m.Name;
			if (m.DependsOn.Any()) title += $" (Required by: {string.Join(',', m.DependsOn.Select(d => Instance.Fabric.Mods.First(m => m.ProjectID == d).Name))})";

			var i = InstalledList.AddItem(title);
			InstalledList.SetItemIcon(i, PlaceholderModIcon);

			async void LoadIcon()
			{
				var ic = (await WebCache.Get(m.Icon)).LoadTexture(m.Icon.Split('.').Last());
				InstalledList.SetItemIcon(i, ic ?? PlaceholderModIcon);
			}

			LoadIcon();
		}

		if (Hits == null || Hits.Count == 0)
			SubmitSearch("");
	}

	async void InstallFabric()
	{
		InstallFabricButton.Visible = false;
		InstallerLabel.Text = "Getting loader";
		Spinner.Visible = true;

		var ls = await FabricMeta.GetLoaders(Instance.Version.Id);
		var l = ls.FirstOrDefault(l => l.Loader.Stable);

		if (l == null)
		{
			InstallerLabel.Text = "Fabric is not available for this version";
			Spinner.Visible = false;
			return;
		}

		InstallerStatus status = InstallerStatus.Invalid;
		Instance.Fabric = await Installer.DownloadFabric(Instance, l, (o)=>
		{
			Dictionary<InstallerStatus, string> dict = new()
			{
				[InstallerStatus.FabricDownloadingLoader] = "Downloading Fabric loader",
				[InstallerStatus.FabricDownloadingLibraries] = "Downloading libraries",
			};

			if (o is InstallerStatus s)
			{
				status = s;

				if (dict.ContainsKey(status)) InstallerLabel.Text = dict[status];
				else InstallerLabel.Text = status.ToString();
			}

			if (o is InstallerDownload d)
			{
				if (d.Size != 0) InstallerSubLabel.Text = $"{d.Name} ({d.Size.SizeToString()})";
				else InstallerSubLabel.Text = d.Name;
			}
		});

		await Instance.Save();

		OnFabricInstalled();
	}

	bool searching = false;
	async void SubmitSearch(string query)
	{
		if (searching) return;
		searching = true;

		SearchList.Clear();

		var s = await Modrinth.Search(query, new (string, string)[] { ("versions", Instance.Meta.Id), ("categories", "fabric"), ("project_type","mod") }, limit: 50);
		Hits = s.Hits;

		foreach (var m in Hits)
		{
			var title = $"{m.Title} ({m.Downloads} DLs)";

			{
				var im = Instance.Fabric.Mods.FirstOrDefault(im => im.ProjectID == m.ProjectID);
				if (im != null) title += $" (Installed)";
			}

			var i = SearchList.AddItem(title);
			SearchList.SetItemIcon(i, PlaceholderModIcon);

			async void DownloadIcon()
			{
				if (string.IsNullOrEmpty(m.IconUrl)) return;

				var ext = m.IconUrl.Split('.').Last();
				var imgstream = await WebCache.Get(m.IconUrl);
				var tex = imgstream.LoadTexture(ext);
				SearchList.SetItemIcon(i, tex ?? PlaceholderModIcon);
			}

			DownloadIcon();
		}

		searching = false;
	}

	async void DownloadSelected()
	{
		Directory.CreateDirectory(Instance.GetModsDirectory());

		// There's probably a proper way to do this but I forgor
		var mods = SearchList.GetSelectedItems().Select(s => Hits[s]);

		await Installer.DownloadMods(Instance, mods.Select(m=>(m, (ModrinthModVersion)null)), (o) =>
		{
			if(o is InstallerDownload d)
			{
				GD.Print($"{d.Name} ({d.Size.SizeToString()})");
			}
		});
		
		GD.Print("Done");
		OnFabricInstalled();

		await Instance.Save();
	}

	async void UninstallSelected()
	{
		var mods = InstalledList.GetSelectedItems()
			.Where(s => s >= 0 && s < Instance.Fabric.Mods.Count)
			.Select(s => Instance.Fabric.Mods[s]);

		void UninstallMod(Mod m)
		{
			foreach (var di in m.Dependencies)
			{
				var d = Instance.Fabric.Mods.FirstOrDefault(m => m.ProjectID == di.ProjectID);
				if (d == null) continue;

				d.DependsOn.Remove(m.ProjectID);

				if (!d.DependsOn.Any())
					UninstallMod(d);
			}

			File.Delete(Instance.GetModsDirectory() + "/" + m.File);
			Instance.Fabric.Mods.Remove(m);
		}

		foreach (var m in mods)
			UninstallMod(m);

		await Instance.Save();

		OnFabricInstalled();
	}
}
