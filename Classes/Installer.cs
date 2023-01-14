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
		var instancedir = instance.GetDirectory();
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
						callback(new InstallerDownload()
						{
							Name = outpath.Split('/').Last(),
							Size = lib.Downloads.Artifact.Size
						});

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

							callback(new InstallerDownload()
							{
								Name = classifier.Path.Split('/').Last(),
								Size = classifier.Size
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
				Name = outfile.Split('/').Last()
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