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
	Label InfoLabel, SubInfoLabel;
	[Export]
	Button DownloadButton;
	[Export]
	CheckBox Release, Snapshot, Alpha, Beta;
	[Export]
	ItemList ModpackList;

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
		var instance = new Instance()
		{
			Id = v.Id,
			Version = v,
			Name = v.Id,
			Meta = meta,
		};

		var objectsdir = Paths.AssetsObjects;
		var indexesdir = Paths.AssetsIndexes;
		var instancedir = instance.GetDirectory();
		var mcdir = instance.GetMinecraftDirectory();

		Directory.CreateDirectory(objectsdir);
		Directory.CreateDirectory(mcdir);

		InfoLabel.Text = "Getting asset index";
		// Download assets
		{
			var assetIndex = meta.AssetIndex;

			var index = await MinecraftLauncher.GetAssetIndex(meta.AssetIndex.URL);
			var objects = index.Objects;

			// Save index json
			{
				System.IO.Directory.CreateDirectory(indexesdir);
				await System.IO.File.WriteAllTextAsync(indexesdir + "/" + assetIndex.Id + ".json",
					JsonSerializer.Serialize(index));
			}

			InfoLabel.Text = "Downloading assets";
			var toDownload = objects.Where(o =>
			{
				var v = o.Value;
				var outpath = objectsdir + "/" + v.Hash[0..2] + "/" + v.Hash;

				var name = (string)o.Key;

				// Reduce download size by not downloading extra languages (~50MB)
				var selectedLang = "en_us";
				if (name.StartsWith("minecraft/lang/"))
				{
					if (name != "minecraft/lang/" + selectedLang + ".json")
					{
						return false;
					}
				}

				return !System.IO.File.Exists(outpath);
			}).OrderByDescending(d => d.Value.Size).ToList();

			long dlSize = toDownload.Sum(d => d.Value.Size);
			long dlProgress = 0;

			InfoLabel.Text = $"Downloading assets (0B/{dlSize.BytesToString()}, 0%)";

			// https://wiki.vg/Game_files
			foreach (var d in toDownload)
			{
				string url = "http://resources.download.minecraft.net/" + d.Value.Hash[0..2] + "/" + d.Value.Hash;
				var outpath = objectsdir + "/" + d.Value.Hash[0..2] + "/" + d.Value.Hash;

				SubInfoLabel.Text = d.Key;

				var fi = new System.IO.FileInfo(outpath);
				System.IO.Directory.CreateDirectory(fi.DirectoryName);

				var res = await new RequestBuilder(url).Get<Stream>();
				var f = System.IO.File.OpenWrite(outpath);
				await res.CopyToAsync(f);
				f.Close();

				dlProgress += res.Length;
				var precentage = ((dlProgress / (float)dlSize) * 100).ToString("0.0");

				InfoLabel.Text = $"Downloading assets ({dlProgress.BytesToString()}/{dlSize.BytesToString()}, {precentage}%)";
			}
		}

		// Download client jar
		{
			InfoLabel.Text = "Downloading client";
			SubInfoLabel.Text = "";

			var url = meta.Downloads.Client.Url;

			var outpath = mcdir + "/client.jar";
			if (!System.IO.File.Exists(outpath))
			{
				var fi = new System.IO.FileInfo(outpath);
				System.IO.Directory.CreateDirectory(fi.DirectoryName);

				var jar = await new RequestBuilder(url).Get<Stream>();
				var f = System.IO.File.OpenWrite(outpath);
				jar.CopyTo(f);
				f.Close();
			}
		}

		string osName = "linux";
		string osVersion = null;
		string archName = "x86";
		List<string> features = new();

		// Download libraries
		{
			InfoLabel.Text = "Downloading libraries";
			SubInfoLabel.Text = "";

			foreach (var lib in meta.Libraries)
			{
				if (lib.Downloads.Artifact != null)
				{

					var outpath = mcdir + "/libs/" + lib.Downloads.Artifact.Path;
					var dlurl = lib.Downloads.Artifact.Url;

					if (lib.Rules != null)
					{
						var allow = !lib.Rules.Select(r => r.Allowed(osName, null, archName, features))
							.Any(r => !r);
						if (!allow) continue;
					}

					if (!System.IO.File.Exists(outpath))
					{
						SubInfoLabel.Text = outpath.Split('/').Last();

						var fi = new System.IO.FileInfo(outpath);
						System.IO.Directory.CreateDirectory(fi.DirectoryName);

						var dat = await new RequestBuilder(dlurl).Get<Stream>();
						var f = System.IO.File.OpenWrite(outpath);
						await dat.CopyToAsync(f);
						f.Close();
					}
				}

				// 1.18 seems to stop including it
				if (lib.Natives != null)
				{
					var natives = lib.Natives;
					if (natives.ContainsKey(osName))
					{
						var classifiersName = (string)natives[osName];
						if (lib.Downloads.Classifiers.ContainsKey(classifiersName))
						{
							var classifier = lib.Downloads.Classifiers[classifiersName];

							SubInfoLabel.Text = classifier.Path.Split('/').Last();

							var url = classifier.Url;
							// Java users try not to use .jar for everything challenge (impossible)
							var jar = await new RequestBuilder(url).Get<Stream>();
							var zip = ZipArchive.Open(jar);

							foreach (var e in zip.Entries)
							{
								if (e.IsDirectory) continue;

								bool exclude = false;
								if (lib.ExtractExclude != null)
									foreach (var ex in lib.ExtractExclude)
									{
										if (e.Key.StartsWith(ex))
										{
											exclude = true;
											continue;
										}
									}
								if (exclude) continue;

								var outdir = mcdir + "/natives/" + e.Key;

								var fi = new System.IO.FileInfo(outdir);
								System.IO.Directory.CreateDirectory(fi.DirectoryName);

								var s = e.OpenEntryStream();
								var f = System.IO.File.Open(outdir, System.IO.FileMode.Create);
								await s.CopyToAsync(f);
								f.Close();
							}
						}
					}
				}
			}
		}

		InfoLabel.Text = "Downloading Java";
		SubInfoLabel.Text = "";

		// Download Java
		{
			// Check if a matching major Java version is already installed 
			if (!Java.Versions.Any(v => v.MajorVersion == meta.JavaVersion.MajorVersion))
			{
				// Unlike the launcher, architecture bitness matters
				var javaArch = "x64";

				var a = (await Adoptium.GetJRE(osName, javaArch, meta.JavaVersion.MajorVersion)).First();

				SubInfoLabel.Text = a.ReleaseName;

				var javaname = a.ReleaseName + "-jre";
				var java = new Java()
				{
					Release = javaname,
					MajorVersion = meta.JavaVersion.MajorVersion
				};

				var javadir = java.GetDirectory();
				Directory.CreateDirectory(javadir);

				var tarstream = await new RequestBuilder(a.Binary.Package.Link).Get<Stream>();
				var r = ReaderFactory.Open(tarstream);

				var eo = new ExtractionOptions()
				{
					Overwrite = true,
					WriteSymbolicLink = (source, target) =>
					{
						File.CreateSymbolicLink(source, target);
					}
				};

				while (r.MoveToNextEntry())
				{
					var e = r.Entry;
					if (e.IsDirectory) continue;

					var k = e.Key;

					if (e.Key.StartsWith(javaname))
						k = k[(javaname.Length + 1)..];

					var outdir = javadir + "/" + k;
					Directory.CreateDirectory(Path.GetDirectoryName(outdir));

					r.WriteEntryToFile(outdir, eo);

					// This is awful but Mono Unix syscalls didn't work
					if (e is TarEntry te)
					{
						string PermissionString(long perms)
						{
							string s = "";
							for (int i = 0; i < 3; i++)
							{
								var p = (perms >> (i * 3)) & 0b111;
								s = p.ToString() + s;
							}
							return s;
						}

						var p = new System.Diagnostics.Process();
						p.StartInfo = new System.Diagnostics.ProcessStartInfo()
						{
							FileName = "chmod",
							UseShellExecute = false,
							Arguments = PermissionString(te.Mode) + " " + outdir
						};
						p.Start();
						await p.WaitForExitAsync();
					}
				}

				await java.Save();
			}
		}

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

		var ver = (await Modrinth.GetVersions(mp.ProjectID)).First(v=>v.Loaders.Contains("fabric"));

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

			if(dep.VersionId != null) 
			{
				// Download by version ID
			}
			else if(dep.ProjectId != null)
			{
				// Download latest mod version
			}
			else
			{
				if(File.Exists(dep.FileName))
					GD.Print("Local dependency " + dep.FileName + " not found");
			}
		}

		GetTree().ReloadCurrentScene();
	}
}
