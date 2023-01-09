using System.IO;
using System.Text.Json;
using System.Collections.Generic;

public class Java
{
	public static string InfoFile = "info.json";
	public static List<Java> Versions;
	public static void LoadVersions()
	{
		Versions = new();
		Directory.CreateDirectory(Paths.Java);
		foreach(var d in Directory.GetDirectories(Paths.Java))
		{
			var jpath = d + "/" + InfoFile;
			if(!File.Exists(jpath)) continue;

			Java j = null;
			try
			{
				j = JsonSerializer.Deserialize<Java>(File.ReadAllText(jpath));
			}
			catch(JsonException)
			{
				// Invalid JSON, skip
				continue;
			}
			Versions.Add(j);
		}
	}

	public string GetExecutable()
	{
		return Paths.Java + "/" + Release + "/bin/java";
	}

	public int MajorVersion { get; set; }
	public string Release { get; set; }
}