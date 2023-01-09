using Godot;
using SharpCompress.Archives.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public partial class NewInstance : ColorRect
{
	[Export]
	OptionButton VersionButton;

	MinecraftManifest Manifest;

	public override async void _Ready()
	{
		Manifest = await MinecraftLauncher.GetManifest();
		foreach (var v in Manifest.Versions)
		{
			VersionButton.AddItem(v.VersionType + " " + v.Id);
		}
	}

	async void Download()
	{
		var v = Manifest.Versions[VersionButton.Selected];
		var meta = await MinecraftLauncher.GetVersionMeta(v.Url);
		var instance = new Instance()
		{
			Id = v.Id,
			Version = v,
			Name = v.Id,
			Meta = meta,
		};
		GD.Print(v.Id);

		var objectsdir = Paths.Assets + "/objects";
		var indexesdir = Paths.Assets + "/indexes";
		var instancedir = Paths.Instances + "/" + v.Id;
		var instancefile = Paths.Instances + "/" + v.Id + "/" + Instance.InstanceFile;
		var mcdir = Paths.Instances + "/" + v.Id + "/.minecraft";

		Directory.CreateDirectory(objectsdir);
		Directory.CreateDirectory(mcdir);

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
			GD.Print("Downloading " + toDownload.Count + " objects... (" + dlSize.BytesToString() + ")");
			// https://wiki.vg/Game_files
			foreach (var d in toDownload)
			{
				string url = "http://resources.download.minecraft.net/" + d.Value.Hash[0..2] + "/" + d.Value.Hash;
				var outpath = objectsdir +"/" + d.Value.Hash[0..2] + "/" + d.Value.Hash;

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
		}

		// Download client jar
		{
			GD.Print("Downloading client.jar");

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
			GD.Print("Downloading libraries");

			foreach (var lib in meta.Libraries)
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
					GD.Print(dlurl + " => " + outpath);

					var fi = new System.IO.FileInfo(outpath);
					System.IO.Directory.CreateDirectory(fi.DirectoryName);

					var dat = await new RequestBuilder(dlurl).Get<Stream>();
					var f = System.IO.File.OpenWrite(outpath);
					await dat.CopyToAsync(f);
					f.Close();
				}

				// 1.18 seems to stop including it
				if (lib.Natives != null)
				{
					var natives = lib.Natives;
					if (natives.ContainsKey(osName))
					{
						var classifiersName = (string)natives[osName];
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

		await File.WriteAllTextAsync(instancefile, JsonSerializer.Serialize(instance));
	
		GetTree().ReloadCurrentScene();
	}
}
