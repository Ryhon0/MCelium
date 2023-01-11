using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

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
		foreach (var m in Instance.Fabric.Mods.OrderBy(m=>!m.InstalledExplicitly))
		{
			var title = m.Name;
			if(m.DependsOn.Any()) title += $" (Required by: {string.Join(',', m.DependsOn.Select(d=>Instance.Fabric.Mods.First(m=>m.ProjectID == d).Name))})";

			var i = InstalledList.AddItem(title);
			InstalledList.SetItemIcon(i, PlaceholderModIcon);

			async void LoadIcon()
			{
				var ic = (await WebCache.Get(m.Icon)).LoadTexture(m.Icon.Split('.').Last());
				InstalledList.SetItemIcon(i, ic ?? PlaceholderModIcon);
			}

			LoadIcon();
		}
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

		OnFabricInstalled();
	}

	bool searching = false;
	async void SubmitSearch(string query)
	{
		if (searching) return;
		searching = true;

		SearchList.Clear();

		var s = await Modrinth.Search(query, new (string, string)[] { ("versions", Instance.Meta.Id), ("categories", "fabric") }, limit: 50);
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

		async Task InstallMod(string projectId, string requiredBy = null)
		{
			if (Instance.Fabric.Mods.Any(im => im.ProjectID == projectId))
			{
				GD.Print(projectId + " already installed, skipping");

				if (requiredBy != null)
				{
					Instance.Fabric.Mods.First(m => m.ProjectID == projectId).DependsOn.Add(requiredBy);
				}

				return;
			}

			GD.Print("Downloading " + projectId);
			var p = await Modrinth.GetProject(projectId);
			var v = (await Modrinth.GetVersions(projectId))
				.First(v => v.GameVersions.Contains(Instance.Version.Id) &&
						v.Loaders.Contains("fabric"));

			var f = v.Files.First();
			GD.Print(f.Filename);

			var mi = new Mod()
			{
				Name = p.Title,
				ProjectID = projectId,
				Version = v.Id,
				File = f.Filename,
				Icon = p.IconUrl,
				InstalledExplicitly = requiredBy == null,
				Dependencies = v.Dependencies.Select(d => new ModDependency() 
					{
						ProjectID = d.ProjectId,
						Version = d.VersionId
					}).ToList()
			};
			if(requiredBy != null) mi.DependsOn = new List<string>() { requiredBy };

			var js = await new RequestBuilder(f.Url).Get<Stream>();
			var fs = File.OpenWrite(Instance.GetModsDirectory() + "/" + f.Filename);
			await js.CopyToAsync(fs);
			fs.Close();

			Instance.Fabric.Mods.Add(mi);

			GD.Print(v.Dependencies.Count);
			foreach (var d in v.Dependencies)
				await InstallMod(d.ProjectId, projectId);
		}

		foreach (var m in mods)
		{
			await InstallMod(m.ProjectID);
		}

		GD.Print("Done");
		OnFabricInstalled();

		await Instance.Save();
	}

	async void UninstallSelected()
	{
		var mods = InstalledList.GetSelectedItems().Select(s => Instance.Fabric.Mods[s]);

		void UninstallMod(Mod m)
		{
			foreach(var di in m.Dependencies)
			{
				var d = Instance.Fabric.Mods.First(m=>m.ProjectID == di.ProjectID);
				d.DependsOn.Remove(m.ProjectID);

				if(!d.DependsOn.Any())
					UninstallMod(d);
			}

			File.Delete(Instance.GetModsDirectory() + "/" + m.File);
			Instance.Fabric.Mods.Remove(m);
		}

		foreach(var m in mods)
			UninstallMod(m);
		
		await Instance.Save();

		OnFabricInstalled();
	}
}
