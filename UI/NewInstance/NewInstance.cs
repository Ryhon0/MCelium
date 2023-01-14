using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Common.Tar;
using System.Collections.Generic;
using SharpCompress.Archives.Zip;

public partial class NewInstance : ColorRect
{
	[Export]
	OptionButton VersionButton;
	[Export]
	Control InstallPage, ProgressPage, Spinner;
	[Export]
	Label InfoLabel, SubInfoLabel, ProgressLabel;
	[Export]
	Button DownloadButton;
	[Export]
	CheckBox Release, Snapshot, Alpha, Beta;
	[Export]
	ItemList ModpackList;
	[Export]
	ProgressBar ProgressBar;
	[Export]
	LineEdit InstanceName;

	[Export]
	Texture2D PlaceholderModpackIcon;

	MinecraftManifest Manifest;
	List<ModrinthProject> ModpackHits;

	public override async void _Ready()
	{
		DownloadButton.Disabled = true;

		SearchModpacks("");

		Manifest = await MinecraftLauncher.GetManifest();
		PopulateVersionList();

		DownloadButton.Disabled = false;
	}

	async void Download()
	{
		InstallPage.Hide();
		ProgressPage.Show();

		InfoLabel.Text = "Getting metadata";

		var v = GetFilteredVersions().ToList()[VersionButton.Selected];
		var meta = await MinecraftLauncher.GetVersionMeta(v.Url);
		var name = string.IsNullOrEmpty(InstanceName.Text.Trim()) ? v.Id : InstanceName.Text.Trim();
		var instance = new Instance()
		{
			Id = Utils.GetRandomHexString(8),
			Version = v,
			Name = name,
			Meta = meta,
		};

		InstallerStatus status = InstallerStatus.Invalid;
		await Installer.DownloadVersion(instance, (o) =>
		{
			SubInfoLabel.Text = "";

			Dictionary<InstallerStatus, string> dict = new()
			{
				[InstallerStatus.DownloadingClient] = "Downloading client",
				[InstallerStatus.DownloadingIndex] = "Downloading asset index",
				[InstallerStatus.DownloadingAssets] = "Downloading assets",
				[InstallerStatus.DownloadingLibraries] = "Downloading libraries",
				[InstallerStatus.DownloadingJava] = "Downloading Java",
			};

			if (o is InstallerStatus s)
			{
				status = s;

				if (dict.ContainsKey(status)) InfoLabel.Text = dict[status];
				else InfoLabel.Text = status.ToString();
			}

			if (o is InstallerDownload d)
			{
				if (d.Size != 0) SubInfoLabel.Text = $"{d.Name} ({d.Size.SizeToString()})";
				else SubInfoLabel.Text = d.Name;

				if (d.TotalSize > 0)
				{
					ProgressBar.Show();
					ProgressLabel.Show();
					Spinner.Hide();

					ProgressBar.MinValue = 0;
					ProgressBar.MaxValue = d.TotalSize;
					ProgressBar.Value = d.Progress;

					ProgressLabel.Text = $"{d.Progress.SizeToString()}/{d.TotalSize.SizeToString()}";
				}
				else
				{
					ProgressBar.Hide();
					ProgressLabel.Hide();
					Spinner.Show();
				}
			}
		});

		await instance.Save();

		GetTree().ReloadCurrentScene();
	}

	void OnReleaseTypeChanged(bool b)
	{
		PopulateVersionList();
	}

	void PopulateVersionList()
	{
		VersionButton.Clear();
		foreach (var v in GetFilteredVersions())
		{
			VersionButton.AddItem(v.VersionType + " " + v.Id);
		}
	}

	IEnumerable<MinecraftVersion> GetFilteredVersions()
	{
		foreach (var v in Manifest.Versions)
		{
			switch (v.VersionType)
			{
				case MinecraftVersion.VersionTypeRelease:
					if (Release.ButtonPressed)
						yield return v;
					break;

				case MinecraftVersion.VersionTypeSnapshot:
					if (Snapshot.ButtonPressed)
						yield return v;
					break;

				case MinecraftVersion.VersionTypeAlpha:
					if (Alpha.ButtonPressed)
						yield return v;
					break;

				case MinecraftVersion.VersionTypeBeta:
					if (Beta.ButtonPressed)
						yield return v;
					break;

				default:
					yield return v;
					break;
			}
		}
	}

	async void SearchModpacks(string q)
	{
		ModpackList.Clear();

		ModpackHits = (await Modrinth.Search(q, new (string, string)[] { ("project_type", "modpack"), ("categories", "fabric") }, limit: 50)).Hits;

		foreach (var mp in ModpackHits)
		{
			var i = ModpackList.AddItem(mp.Title);
			ModpackList.SetItemIcon(i, PlaceholderModpackIcon);

			async void GetIcon()
			{
				ModpackList.SetItemIcon(i, (await WebCache.Get(mp.IconUrl)).LoadTexture(mp.IconUrl.Split('.').Last()) ?? PlaceholderModpackIcon);
			}

			GetIcon();
		}
	}

	async void DownloadModpack()
	{
		var sel = ModpackList.GetSelectedItems();
		if (sel.Length == 0) return;

		var mp = ModpackHits[sel[0]];

		InstallPage.Hide();
		ProgressPage.Show();

		InfoLabel.Text = "Downloading " + mp.Title + "...";

		var ver = (await Modrinth.GetVersions(mp.ProjectID)).First(v => v.Loaders.Contains("fabric"));

		var f = ver.Files.FirstOrDefault(f => f.Primary) ?? ver.Files.First();

		var mrpackStream = await new RequestBuilder(f.Url).Get<Stream>();
		var mrpack = ZipArchive.Open(mrpackStream);

		var ie = mrpack.Entries.First(e => e.Key == "modrinth.index.json");
		var index = JsonSerializer.Deserialize<MRPackIndex>(ie.OpenEntryStream());

		if (index.Dependencies.ContainsKey("forge"))
		{
			InfoLabel.Text = "Forge modpacks are currently not supported";
			Spinner.Hide();
			return;
		}
		if (index.Dependencies.ContainsKey("quilt-loader"))
		{
			InfoLabel.Text = "Quilt modpacks are currently not supported";
			Spinner.Hide();
			return;
		}

		// Download minecraft
		var mcver = index.Dependencies["minecraft"];
		{
			SubInfoLabel.Text = "Downloading Minecraft " + mcver;

			var lv = (await MinecraftLauncher.GetManifest()).Versions.First(v => v.Id == mcver);
			var meta = await MinecraftLauncher.GetVersionMeta(lv.Url);
		}

		// Install fabric
		{
			var fabricver = index.Dependencies["fabric-loader"];
			SubInfoLabel.Text = "Downloading Fabric " + fabricver;

			var lmeta = await FabricMeta.GetLoader(mcver, fabricver);
		}

		// Extract overrides/ directory
		{

		}

		// Download extra files
		foreach (var inf in index.Files)
		{
			if (inf.Env != null)
			{
				if (inf.Env.ContainsKey("client") &&
					inf.Env["client"] == "unsupported") continue;
			}

			SubInfoLabel.Text = "Downloading " + inf.Path;
			// var s = await new RequestBuilder(inf.Downloads.Random()).Get<Stream>();
			// var outf = File.OpenWrite(inf.Path);
			// await s.CopyToAsync(outf);
			// outf.Close();
		}

		// Download Fabric dependencies
		foreach (var dep in ver.Dependencies)
		{
			if (dep.DependencyType != "required") continue;

			if (dep.VersionId != null)
			{
				// Download by version ID
			}
			else if (dep.ProjectId != null)
			{
				// Download latest mod version
			}
			else
			{
				if (File.Exists(dep.FileName))
					GD.Print("Local dependency " + dep.FileName + " not found");
			}
		}

		GetTree().ReloadCurrentScene();
	}
}
