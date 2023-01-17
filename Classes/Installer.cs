using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Readers;

public static class Installer
{
	public static async Task DownloadVersion(Instance instance, Action<object> callback)
	{
		var meta = instance.Meta;
		var objectsdir = Paths.AssetsObjects;
		var indexesdir = Paths.AssetsIndexes;
		var mcdir = instance.GetMinecraftDirectory();

		Directory.CreateDirectory(objectsdir);
		Directory.CreateDirectory(mcdir);


		// Download assets
		{
			callback(InstallerStatus.DownloadingIndex);
			var assetIndex = meta.AssetIndex;

			var index = await MinecraftLauncher.GetAssetIndex(meta.AssetIndex.URL);
			var objects = index.Objects;

			// Save index json
			{
				System.IO.Directory.CreateDirectory(indexesdir);
				await System.IO.File.WriteAllTextAsync(indexesdir + "/" + assetIndex.Id + ".json",
					JsonSerializer.Serialize(index));
			}

			callback(InstallerStatus.DownloadingAssets);
			// InfoLabel.Text = "Downloading assets";
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

			// InfoLabel.Text = $"Downloading assets (0B/{dlSize.BytesToString()}, 0%)";

			// https://wiki.vg/Game_files
			foreach (var d in toDownload)
			{
				callback(new InstallerDownload()
				{
					Name = d.Key,
					Size = d.Value.Size,
					Progress = dlProgress,
					TotalSize = dlSize
				});

				string url = "http://resources.download.minecraft.net/" + d.Value.Hash[0..2] + "/" + d.Value.Hash;
				var outpath = objectsdir + "/" + d.Value.Hash[0..2] + "/" + d.Value.Hash;

				var fi = new System.IO.FileInfo(outpath);
				System.IO.Directory.CreateDirectory(fi.DirectoryName);

				var res = await new RequestBuilder(url).Get<Stream>();
				var f = System.IO.File.OpenWrite(outpath);
				await res.CopyToAsync(f);
				f.Close();

				dlProgress += res.Length;
				var precentage = ((dlProgress / (float)dlSize) * 100).ToString("0.0");

				// InfoLabel.Text = $"Downloading assets ({dlProgress.BytesToString()}/{dlSize.BytesToString()}, {precentage}%)";
			}
		}

		// Download client jar
		{
			callback(InstallerStatus.DownloadingClient);
			callback(new InstallerDownload()
			{
				Name = "client.jar",
				Size = meta.Downloads.Client.Size
			});

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
			callback(InstallerStatus.DownloadingLibraries);

			long totalSize = 0;
			long progress = 0;
			var libs = meta.Libraries.Where(l =>
			{
				if (l.Downloads.Artifact != null)
				{
					if (l.Rules != null)
					{
						var allow = !l.Rules.Select(r => r.Allowed(osName, null, archName, features))
							.Any(r => !r);
						if (!allow) return false;
					}

					totalSize += l.Downloads.Artifact.Size;
				}

				if (l.Natives != null)
				{
					var natives = l.Natives;
					if (natives.ContainsKey(osName))
					{
						var classifiersName = (string)natives[osName];
						if (l.Downloads.Classifiers.ContainsKey(classifiersName))
						{
							var classifier = l.Downloads.Classifiers[classifiersName];
							totalSize += classifier.Size;
						}
					}

				}

				return true;
			});

			foreach (var lib in libs.OrderByDescending(l =>).ToList())
			{
				if (lib.Downloads.Artifact != null)
				{
					var outpath = mcdir + "/libs/" + lib.Downloads.Artifact.Path;
					var dlurl = lib.Downloads.Artifact.Url;

					if (!System.IO.File.Exists(outpath))
					{
						callback(new InstallerDownload()
						{
							Name = outpath.Split('/').Last(),
							Size = lib.Downloads.Artifact.Size,
							TotalSize = totalSize,
							Progress = progress
						});

						var fi = new System.IO.FileInfo(outpath);
						System.IO.Directory.CreateDirectory(fi.DirectoryName);

						var dat = await new RequestBuilder(dlurl).Get<Stream>();
						var f = System.IO.File.OpenWrite(outpath);
						await dat.CopyToAsync(f);
						f.Close();

						progress += lib.Downloads.Artifact.Size;
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

							callback(new InstallerDownload()
							{
								Name = classifier.Path.Split('/').Last(),
								Size = classifier.Size,
								TotalSize = totalSize,
								Progress = progress
							});

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

							progress += classifier.Size;
						}
					}
				}
			}
		}

		// Download Java
		{
			// Check if a matching major Java version is already installed 
			if (!Java.Versions.Any(v => v.MajorVersion == meta.JavaVersion.MajorVersion))
			{
				callback(InstallerStatus.DownloadingJava);

				// Unlike the launcher, architecture bitness matters
				var javaArch = "x64";

				var a = (await Adoptium.GetJRE(osName, javaArch, meta.JavaVersion.MajorVersion)).First();

				callback(new InstallerDownload()
				{
					Name = a.ReleaseName,
					Size = a.Binary.Package.Size
				});

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
	}

	public static async Task<InstanceFabricInfo> DownloadFabric(Instance instance, FabricMetaLoaderVersion l, Action<object> callback)
	{
		Directory.CreateDirectory(instance.GetFabricDirectory());

		var fi = new InstanceFabricInfo()
		{
			Version = l.Loader.Version,
			LauncherMeta = l.LauncherMeta
		};


		async Task DownloadLib(FabricMetaLibrary l)
		{
			fi.Libraries.Add(l.Name);

			var outfile = instance.GetFabricDirectory() + "/" + l.Name.Replace(':', '-').Replace(".", "-") + ".jar";
			if (File.Exists(outfile)) return;

			callback(new InstallerDownload()
			{
				Name = l.Name
			});

			var s = await new RequestBuilder(l.GetDownloadUrl()).Get<Stream>();
			var f = File.OpenWrite(outfile);
			await s.CopyToAsync(f);
			f.Close();
		}

		callback(InstallerStatus.FabricDownloadingLoader);
		// Download Loader
		{
			await DownloadLib(new FabricMetaLibrary() { Name = l.Loader.Maven, Url = FabricMeta.MavenUrl });
			await DownloadLib(new FabricMetaLibrary() { Name = l.Intermediary.Maven, Url = FabricMeta.MavenUrl });
		}

		callback(InstallerStatus.FabricDownloadingLibraries);
		// Download libraries
		{
			foreach (var lib in l.LauncherMeta.Libraries.Client)
				await DownloadLib(lib);

			foreach (var lib in l.LauncherMeta.Libraries.Common)
				await DownloadLib(lib);
		}

		return fi;
	}

	public static async Task DownloadMods(Instance instance, IEnumerable<(ModrinthProject p, ModrinthModVersion v)> mods, Action<object> callback)
	{

		async Task InstallMod(ModrinthProject p, ModrinthModVersion v, string requiredBy = null)
		{
			p = p ?? await Modrinth.GetProject(v.ProjectId);

			if (instance.Fabric.Mods.Any(im => im.ProjectID == p.ProjectID))
			{
				// GD.Print(p.ProjectID + " already installed, skipping");

				if (requiredBy != null)
				{
					instance.Fabric.Mods.First(m => m.ProjectID == p.ProjectID).DependsOn.Add(requiredBy);
				}

				return;
			}

			if (v == null)
			{
				v = (await Modrinth.GetVersions(p.ProjectID))
				.First(v => v.GameVersions.Contains(instance.Version.Id) &&
						v.Loaders.Contains("fabric"));
			}

			foreach (var d in v.Dependencies)
			{
				switch (d.DependencyType)
				{
					case "required":
						if (d.ProjectId != null) await InstallMod(await Modrinth.GetProject(d.ProjectId), null, p.ProjectID);
						else await InstallMod(null, await Modrinth.GetVersion(d.VersionId), p.ProjectID);
						break;
					case "incompatible":
						var icm = instance.Fabric.Mods.FirstOrDefault(m => m.ProjectID == d.ProjectId);
						if (icm != null)
						{
							// GD.Print($"{p.Title} is incopatible with {icm.Name}, not installing");
							continue;
						}
						break;
					case "embedded":
						// TODO: handle this
						// GD.Print($"{p.Title} provides {d.ProjectId}");
						break;
				}
			}

			var f = v.Files.First();

			callback(new InstallerDownload()
			{
				Name = p.Title,
				Size = f.Size
			});

			var mi = new Mod()
			{
				Name = p.Title,
				ProjectID = p.ProjectID,
				Version = v.Id,
				File = f.Filename,
				Icon = p.IconUrl,
				InstalledExplicitly = requiredBy == null,
				Dependencies = v.Dependencies.Where(d => d.DependencyType == "required").Select(d => new ModDependency()
				{
					ProjectID = d.ProjectId,
					Version = d.VersionId
				}).ToList()
			};
			if (requiredBy != null) mi.DependsOn = new List<string>() { requiredBy };

			var js = await new RequestBuilder(f.Url).Get<Stream>();
			var fs = File.OpenWrite(instance.GetModsDirectory() + "/" + f.Filename);
			await js.CopyToAsync(fs);
			fs.Close();

			instance.Fabric.Mods.Add(mi);
		}

		foreach (var m in mods)
		{
			await InstallMod(m.p, m.v);
		}
	}

	public static async Task<Instance> DownloadModpack(ModrinthProject mp, Action<object> callback)
	{
		var instance = new Instance()
		{
			Id = Utils.GetRandomHexString(8)
		};

		callback(ModpackInstallerStatus.DownloadingModpack);

		var ver = (await Modrinth.GetVersions(mp.ProjectID)).First(v => v.Loaders.Contains("fabric"));
		var f = ver.Files.FirstOrDefault(f => f.Primary) ?? ver.Files.First();

		var mrpackStream = await new RequestBuilder(f.Url).Get<Stream>();
		var mrpack = ZipArchive.Open(mrpackStream);

		var ie = mrpack.Entries.First(e => e.Key == "modrinth.index.json");
		var index = JsonSerializer.Deserialize<MRPackIndex>(ie.OpenEntryStream());
		instance.Name = index.Name;

		if (index.Dependencies.ContainsKey("forge"))
		{
			throw new Exception("Forge modpacks are currently not supported");
		}
		if (index.Dependencies.ContainsKey("quilt-loader"))
		{
			throw new Exception("Quilt modpacks are currently not supported");
		}

		callback(ModpackInstallerStatus.DownloadingMinecraft);
		// Download minecraft
		var mcver = index.Dependencies["minecraft"];
		{
			// SubInfoLabel.Text = "Downloading Minecraft " + mcver;

			instance.Version = (await MinecraftLauncher.GetManifest()).Versions.First(v => v.Id == mcver);
			instance.Meta = await MinecraftLauncher.GetVersionMeta(instance.Version.Url);

			await Installer.DownloadVersion(instance, (o) => callback(o));
		}

		callback(ModpackInstallerStatus.DownloadingFabric);
		// Install fabric
		{
			var fabricver = index.Dependencies["fabric-loader"];
			var lmeta = await FabricMeta.GetLoader(mcver, fabricver);

			instance.Fabric = await Installer.DownloadFabric(instance, lmeta, (o => callback(o)));
		}

		callback(ModpackInstallerStatus.ExtractingOverrides);
		// Extract overrides/ directory
		foreach (var e in mrpack.Entries.Where(e => e.Key.StartsWith("overrides/") && !e.IsDirectory))
		{
			var outpath = Path.GetFullPath(instance.GetMinecraftDirectory() + "/" + e.Key["overrides/".Length..]);

			if (!outpath.StartsWith(instance.GetMinecraftDirectory() + "/"))
			{
				// Modpack tried to extract files outside the .minecraft directory!!!!!
				continue;
			}

			var estream = e.OpenEntryStream();
			Directory.CreateDirectory(Path.GetDirectoryName(outpath));

			var fs = File.OpenWrite(outpath);
			await estream.CopyToAsync(fs);
			fs.Close();
		}

		callback(ModpackInstallerStatus.DownloadingAdditionalFiles);
		// Download extra files
		foreach (var inf in index.Files)
		{
			if (inf.Env != null)
			{
				if (inf.Env.ContainsKey("client") &&
					inf.Env["client"] == "unsupported") continue;
			}

			var outpath = Path.GetFullPath(instance.GetMinecraftDirectory() + "/" + inf.Path);
			if (!outpath.StartsWith(instance.GetMinecraftDirectory() + "/"))
			{
				// Modpack tried to extract files outside the .minecraft directory!!!!!
				continue;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(outpath));

			callback(new InstallerDownload()
			{
				Name = inf.Path,
				Size = inf.FileSize
			});

			var s = await new RequestBuilder(inf.Downloads.Random()).Get<Stream>();

			var fs = File.OpenWrite(outpath);
			await s.CopyToAsync(fs);
			fs.Close();
		}

		callback(ModpackInstallerStatus.DownloadingMods);
		// Download Fabric dependencies

		List<(ModrinthProject p, ModrinthModVersion v)> mods = new();
		foreach (var d in ver.Dependencies)
		{
			if (d.DependencyType != "required" && d.DependencyType != "embedded") continue;

			ModrinthProject p = null;
			ModrinthModVersion v = null;

			if (d.VersionId == null && d.ProjectId == null) continue;

			v = await Modrinth.GetVersion(d.VersionId);
			p = await Modrinth.GetProject(v.ProjectId);

			if (p == null || v == null) continue;

			if (d.DependencyType == "required")
				mods.Add((p, v));
			else if (d.DependencyType == "embedded")
			{
				instance.Fabric.Mods.Add(new Mod()
				{
					Name = p.Title,
					ProjectID = p.ProjectID,
					Icon = p.IconUrl,
					Version = v.Id,
					File = v.Files.First().Filename,
					InstalledExplicitly = true,

					// TODO: Dependencies and DependsOn
				});
			}
		}
		await Installer.DownloadMods(instance, mods, (o) => callback(o));

		return instance;
	}
}

public class InstallerDownload
{
	public string Name { get; set; }
	public long Size { get; set; }

	public long Progress { get; set; }
	public long TotalSize { get; set; }
}

public enum InstallerStatus
{
	Invalid,
	DownloadingIndex,
	DownloadingAssets,
	DownloadingClient,
	DownloadingLibraries,
	DownloadingJava,

	FabricDownloadingLoader,
	FabricDownloadingLibraries,
}

public enum ModpackInstallerStatus
{
	Invalid,
	DownloadingModpack,
	DownloadingMinecraft,
	DownloadingFabric,
	DownloadingMods,
	ExtractingOverrides,
	DownloadingAdditionalFiles,
}