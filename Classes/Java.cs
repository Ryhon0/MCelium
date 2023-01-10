using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

public class Java
{
	public static string InfoFile = "info.json";
	public static List<Java> Versions;
	public static async Task<List<Java>> LoadVersions()
	{
		var vers = new List<Java>();
		Directory.CreateDirectory(Paths.Java);
		foreach(var d in Directory.GetDirectories(Paths.Java))
		{
			var jpath = d + "/" + InfoFile;
			if(!File.Exists(jpath)) continue;

			Java j = null;
			try
			{
				j = JsonSerializer.Deserialize<Java>(await File.ReadAllTextAsync(jpath));
			}
			catch(JsonException)
			{
				// Invalid JSON, skip
				continue;
			}
			
			vers.Add(j);
		}

		return vers;
	}

	public async Task Save()
	{
		var instdir = Paths.Java + "/" + Release;
		Directory.CreateDirectory(instdir);
		await File.WriteAllTextAsync(instdir + "/" + InfoFile, JsonSerializer.Serialize(this));
	}

	public string GetDirectory()
	{
		return Paths.Java + "/" + Release;
	}

	public string GetExecutable()
	{
		return GetDirectory() + "/bin/java";
	}

	public int MajorVersion { get; set; }
	public string Release { get; set; }
}