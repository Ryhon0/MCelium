using System.Threading.Tasks;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public static class FabricMeta
{
	static string RootUrl = "https://meta.fabricmc.net";

	public static async Task<List<FabricMetaGameVersion>> GetGameVersions()
	{
		return await new RequestBuilder(RootUrl + "/v2/versions/game")
			.Get<List<FabricMetaGameVersion>>();
	}

	public static async Task<List<FabricMetaLoaderVersion>> GetLoaders(string version)
	{
		return await new RequestBuilder(RootUrl + "/v2/versions/loader/" + version)
			.Get<List<FabricMetaLoaderVersion>>();
	}
}

public class FabricMetaGameVersion
{
	[JsonPropertyName("version")]
	public string Version {get;set;}

	[JsonPropertyName("stable")]
	public bool Stable {get;set;}
}

// ffs... Why do I have to define 10 classes for this
public class FabricMetaLoaderVersion
{
	[JsonPropertyName("loader")]
	public FabricMetaLoaderInfo Loader {get;set;}

	[JsonPropertyName("intermediary")]
	public FabricMetaLoaderInfo Intermediary {get;set;}

	[JsonPropertyName("launcherMeta")]
	public FabricMetaLauncherMeta LauncherMeta {get;set;}
}

public class FabricMetaLoaderInfo
{
	[JsonPropertyName("separator")]
	public string Separator {get;set;}

	[JsonPropertyName("build")]
	public int Build {get;set;}

	[JsonPropertyName("maven")]
	public string Maven {get;set;}

	[JsonPropertyName("version")]
	public string Version {get;set;}

	[JsonPropertyName("stable")]
	public bool Stable {get;set;}
}

public class FabricMetaLauncherMeta
{
	[JsonPropertyName("version")]
	public int Version {get;set;}

	[JsonPropertyName("libraries")]
	public FabricMetaLibraries Libraries {get;set;}

	[JsonPropertyName("mainClass")]
	// This property is not always the same type 😃.
	// Sometimes just a string, sometimes a JSON object with "client" and "server" keys
	public JsonNode MainClass {get;set;}
}

public class FabricMetaLibraries
{
	[JsonPropertyName("client")]
	public List<FabricMetaLibrary> Client {get;set;}

	[JsonPropertyName("common")]
	public List<FabricMetaLibrary> Common {get;set;}

	[JsonPropertyName("server")]
	public List<FabricMetaLibrary> Server {get;set;}
}

public class FabricMetaLibrary
{
	[JsonPropertyName("name")]
	public string Name {get;set;}

	[JsonPropertyName("url")]
	public string Url {get;set;}

	public string GetDownloadUrl()
	{
		var split = Name.Split(":");

		var domain = split[0];
		var lib = split[1];
		var version = split[2];

		return Url + domain.Replace('.','/') + "/" + lib + "/" + version + "/" + lib + "-" + version + ".jar";
	}
}