using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

public class Instance
{
	public static string InstanceFile = "instance.json";

	public static async Task<List<Instance>> LoadInstances()
	{
		var ins = new List<Instance>();

		Directory.CreateDirectory(Paths.Instances);
		foreach (var d in Directory.GetDirectories(Paths.Instances))
		{
			var jsonfile = d + "/" + InstanceFile;
			if (!File.Exists(jsonfile)) continue;

			Instance i = null;
			try
			{
				i = JsonSerializer.Deserialize<Instance>(await System.IO.File.ReadAllTextAsync(jsonfile));
			}
			catch (JsonException je)
			{
				// Invalid JSON, skip instance
				continue;
			}

			ins.Add(i);
		}

		return ins;
	}

	public async Task Save()
	{
		var instdir = GetDirectory();
		Directory.CreateDirectory(instdir);
		await File.WriteAllTextAsync(instdir + "/" + InstanceFile, JsonSerializer.Serialize(this));
	}

	public string GetDirectory()
		=> Paths.Instances + "/" + Id;

	public string GetMinecraftDirectory()
		=> GetDirectory() + "/.minecraft";

	public string GetFabricDirectory()
		=> GetMinecraftDirectory() + "/fabric";

	public string GetModsDirectory()
		=> GetMinecraftDirectory() + "/mods";

	public string Name { get; set; }
	public string Id { get; set; }

	public MinecraftVersion Version { get; set; }
	public MinecraftVersionMeta Meta { get; set; }
	public InstanceFabricInfo Fabric { get; set; }
}

public class InstanceFabricInfo
{
	public string Version { get; set; }
	/// List of maven libraries in the following format: com.example:package:1.0.0
	public List<string> Libraries { get; set; } = new();
	public FabricMetaLauncherMeta LauncherMeta { get; set; }
}