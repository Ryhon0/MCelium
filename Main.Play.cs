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

		string MainClass = meta.MainClass;

		// Download assets
		{
			var assetIndex = meta.AssetIndex;

			var index = await MinecraftLauncher.GetAssetIndex(meta.AssetIndex.URL);
			var objects = index.Objects;

			System.IO.Directory.CreateDirectory(mcdir + "assets/indexes");
			await System.IO.File.WriteAllTextAsync(mcdir + "assets/indexes/" + assetIndex.Id + ".json",
				JsonSerializer.Serialize(index));

			var toDownload = objects.Where(o =>
			{
				var v = o.Value;
				var outpath = mcdir + "assets/objects/" + v.Hash[0..2] + "/" + v.Hash;

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
			})
				.OrderByDescending(d => d.Value.Size).ToList();

			long dlSize = toDownload.Sum(d => d.Value.Size);
			long dlProgress = 0;
			GD.Print("Downloading " + toDownload.Count + " objects... (" + dlSize.BytesToString() + ")");
			// https://wiki.vg/Game_files
			foreach (var d in toDownload)
			{
				string url = "http://resources.download.minecraft.net/" + d.Value.Hash[0..2] + "/" + d.Value.Hash;
				var outpath = mcdir + "assets/objects/" + d.Value.Hash[0..2] + "/" + d.Value.Hash;

				GD.Print(d.Key);

				var fi = new System.IO.FileInfo(outpath);
				System.IO.Directory.CreateDirectory(fi.DirectoryName);

				var res = await new RequestBuilder(url).Get<Stream>();
				var f = System.IO.File.OpenWrite(outpath);
				await res.CopyToAsync(f);
				f.Close();

				dlProgress += res.Length;
				var precentage = ((dlProgress / (float)dlSize) * 100).ToString("0.0");
				GD.Print($"{dlProgress.BytesToString()}/{dlSize.BytesToString()} - {precentage}%");
			}

			GD.Print("Done!");
		}

		// Download client jar
		{
			GD.Print("Downloading client.jar");

			var url = meta.Downloads.Client.Url;

			var outpath = mcdir + "client.jar";
			if (!System.IO.File.Exists(outpath))
			{
				var fi = new System.IO.FileInfo(outpath);
				System.IO.Directory.CreateDirectory(fi.DirectoryName);

				var jar = await new RequestBuilder(url).Get<Stream>();
				var f = System.IO.File.OpenWrite(outpath);
				jar.CopyTo(f);
				f.Close();
			}

			GD.Print("Done!");
		}

		// Download libraries
		{
			GD.Print("Downloading libraries");

			foreach (var lib in meta.Libraries)
			{
				var outpath = mcdir + "libs/" + lib.Downloads.Artifact.Path;
				var dlurl = lib.Downloads.Artifact.Url;

				if (lib.Rules != null)
				{
					var allow = !lib.Rules.Select(r => r.Allowed(osName, null, archName, features))
						.Any(r => !r);
					if (!allow) continue;
				}

				if (!System.IO.File.Exists(outpath))
				{
					GD.Print(dlurl + " => " + outpath);

					var fi = new System.IO.FileInfo(outpath);
					System.IO.Directory.CreateDirectory(fi.DirectoryName);

					var dat = await new RequestBuilder(dlurl).Get<Stream>();
					var f = System.IO.File.OpenWrite(outpath);
					await dat.CopyToAsync(f);
					f.Close();
				}

				// 1.19 seems to stop including it
				if (lib.Natives != null)
				{
					var os = "linux";

					var natives = lib.Natives;
					if (natives.ContainsKey(os))
					{
						var classifiersName = (string)natives[os];
						var classifier = lib.Downloads.Classifiers[classifiersName];

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

							GD.Print("Extracting " + e.Key);
							var outdir = mcdir + "natives/" + e.Key;

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
			GD.Print("Done!");
		}

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

		// Download Java
		/*
		{
			var javaVersion = meta.JavaVersion.MajorVersion;
			string os = "linux";
			string arch = "x64";

			var jstr = await Adoptium.GetJRE(os, arch, javaVersion);
			var adoptiumjson = JSON.ParseString(jstr).AsGodotArray()[0].AsGodotDictionary();
			var dlurl = (string)adoptiumjson["binary"].AsGodotDictionary()["package"].AsGodotDictionary()["link"];

			GD.Print(dlurl);

			// var tar = (await this.Get(dlurl, new string[] { })).result;
		}
		*/


		// Launch
		{
			List<string> args = new() { "-Xms512m", "-Xmx4096m", "-Duser.language=en" };

			IEnumerable<string> ProcessArguments(IEnumerable<JsonNode> nodes)
			{
				foreach (var n in nodes)
				{
					if (n is JsonObject o)
					{
						var rules = JsonSerializer.Deserialize<List<MinecraftRule>>(o["rules"]);
						var allowed = !rules.Select(r => r.Allowed(osName, osVersion, archName, features)).Any(rr => !rr);

						var vo = o["value"];
						if (allowed)
							if (vo is JsonArray a)
							{
								foreach (var ao in a)
									yield return (string)ao;
							}
							else yield return (string)vo;
					}
					else yield return (string)n;
				}
			}

			Dictionary<string, string> replacekeys = new()
			{
				["auth_player_name"] = "Ryhon_",
				["version_name"] = version.Id,
				["game_directory"] = mcdir,
				["assets_root"] = mcdir + "assets",
				["assets_index_name"] = "1.19",
				["auth_uuid"] = uuid,
				["auth_access_token"] = token,
				["clientid"] = "minecraft",
				["auth_xuid"] = xuid,
				["user_type"] = "microsoft",
				["version_type"] = version.VersionType,
				["natives_directory"] = mcdir + "natives",
				["launcher_name"] = "MCelium",
				["launcher_version"] = "0.0.1",
				["classpath"] = String.Join(':', System.IO.Directory.GetFileSystemEntries(mcdir, "*.jar", SearchOption.AllDirectories))
			};

			args.AddRange(ProcessArguments(meta.Arguments.JVM).ToList());
			args.Add(MainClass);
			args.AddRange(ProcessArguments(meta.Arguments.Game).ToList());

			string ReplaceDict(string s, Dictionary<string, string> d)
			{
				string ss = s;
				foreach (var ds in d)
					ss = ss.Replace("${" + ds.Key + "}", ds.Value);

				return ss;
			}

			for (int i = 0; i < args.Count; i++)
				args[i] = ReplaceDict(args[i], replacekeys);

			var psi = new ProcessStartInfo()
			{
				FileName = "java",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			};

			for (int i = 0; i < args.Count; i++)
				psi.ArgumentList.Add(args[i]);

			var p = new Process();
			p.StartInfo = psi;

			GD.Print("Starting...");
			p.Start();
			await p.WaitForExitAsync();
			GD.Print("Exited with code " + p.ExitCode);

			GD.Print(p.StandardError.ReadToEnd());
			GD.Print(p.StandardOutput.ReadToEnd());
		}
	}

}