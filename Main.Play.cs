using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Collections.Generic;
using SharpCompress.Archives.Zip;
using SharpCompress.Archives.Tar;
using System.IO;
using System.Diagnostics;

public partial class Main
{
	async void Play(string token, string uuid, string xuid)
	{
		var manifest = await MinecraftLauncher.GetManifest();
		var version = manifest.Versions.First(v => v.Id == "1.19.2");
		string mcdir = ".minecraft/" + version.Id + "/";

		string osName = "linux";
		string osVersion = null;
		string archName = "x86";
		System.Collections.Generic.List<string> features = new() { };

		var meta = await MinecraftLauncher.GetVersionMeta(version.Url);

		string MainClass = "";
		// Download Fabric
		{
			var loaders = await FabricMeta.GetLoaders(meta.Id);

			var l = loaders.First(l => l.Loader.Stable);

			// This is so awesome, thank you 😃
			if (l.LauncherMeta.MainClass is JsonObject o)
				MainClass = (string)(o["client"]);
			else MainClass = (string)l.LauncherMeta.MainClass;

			Directory.CreateDirectory(mcdir + "fabric");

			async Task DownloadLoader(FabricMetaLoaderInfo loader)
			{
				var l = new FabricMetaLibrary()
				{
					Name = loader.Maven,
					Url = "https://maven.fabricmc.net/"
				};

				var outfile = mcdir + "fabric/" + l.Name.Replace(':', '-') + ".jar";

				GD.Print(l.GetDownloadUrl());
				var s = await new RequestBuilder(l.GetDownloadUrl()).Get<Stream>();
				var f = File.OpenWrite(outfile);
				await s.CopyToAsync(f);
				f.Close();
			}

			await DownloadLoader(l.Loader);
			await DownloadLoader(l.Intermediary);

			// Libraries
			async Task DownloadLibraries(List<FabricMetaLibrary> libs)
			{
				foreach (var l in libs)
				{
					var outfile = mcdir + "fabric/" + l.Name.Replace(':', '-') + ".jar";

					if (File.Exists(outfile)) continue;

					GD.Print(l.Name);
					var s = await new RequestBuilder(l.GetDownloadUrl()).Get<Stream>();

					var f = File.OpenWrite(outfile);
					await s.CopyToAsync(f);
					f.Close();
				}
			}

			await DownloadLibraries(l.LauncherMeta.Libraries.Client);
			await DownloadLibraries(l.LauncherMeta.Libraries.Common);
		}

		// Download some mods
		{
			Directory.CreateDirectory(mcdir + "mods");

			async Task DownloadMod(string query, string version, string loader)
			{
				var s = await Modrinth.Search(query, new (string, string)[] { ("versions", version), ("categories", loader) });

				var mod = s.Hits.First();
				// GD.Print($"{mod.Title} - https://modrinth.com/{mod.ProjectType}/{mod.Slug}");

				var vers = await Modrinth.GetVersions(mod.ProjectID);
				var v = vers.First(v => v.GameVersions.Contains(version) && v.Loaders.Contains(loader));

				var f = v.Files.First(f => f.Primary);
				GD.Print("Download " + f.Url);

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
						GD.Print("Download " + df.Url);

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
		}
	}
}