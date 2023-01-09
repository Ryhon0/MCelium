using System;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public static class MinecraftLauncher
{
	public static async Task<MinecraftManifest> GetManifest()
	{
		return await new RequestBuilder("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json")
			.Header("Accept", "application/json")
			.Get<MinecraftManifest>();
	}

	public static async Task<MinecraftVersionMeta> GetVersionMeta(string url)
	{	
		return await new RequestBuilder(url)
			.Header("Accept", "application/json")
			.Get<MinecraftVersionMeta>();
	}

	public static async Task<MinecraftAssetIndex> GetAssetIndex(string url)
	{
		return await new RequestBuilder(url)
			.Header("Accept", "application/json")
			.Get<MinecraftAssetIndex>();
	}
}

public class MinecraftManifest
{
	[JsonPropertyName("latest")]
	public MinecraftLatestVersions Latest { get; set; }
	[JsonPropertyName("versions")]
	public System.Collections.Generic.List<MinecraftVersion> Versions { get; set; }
}

// It would be SO cool if you could give nested classes JsonPropertyNameAttribute
public class MinecraftLatestVersions
{
	[JsonPropertyName("release")]
	public string Release { get; set; }
	[JsonPropertyName("snapshot")]
	public string Snapshot { get; set; }
}

public class MinecraftVersion
{
	public const string VersionTypeRelease = "release";
	public const string VersionTypeSnapshot = "snapshot";
	public const string VersionTypeBeta = "old_beta";
	public const string VersionTypeAlpha = "old_alpha";

	[JsonPropertyName("id")]
	public string Id { get; set; }
	[JsonPropertyName("type")]
	public string VersionType { get; set; }
	[JsonPropertyName("url")]
	public string Url { get; set; }
	[JsonPropertyName("time")]
	public DateTime UpdatedAt { get; set; }
	[JsonPropertyName("releaseTime")]
	public DateTime ReleasedAt { get; set; }
	[JsonPropertyName("sha1")]
	public byte[] Sha1 { get; set; }
	[JsonPropertyName("complianceLevel")]
	public int ComplianceLevel { get; set; }
}

public class MinecraftAssetIndexInfo
{
	[JsonPropertyName("id")]
	public string Id { get; set; }
	[JsonPropertyName("sha1")]
	public byte[] Sha1 { get; set; }
	[JsonPropertyName("size")]
	public long Size { get; set; }
	[JsonPropertyName("totalSize")]
	public long TotalSize { get; set; }
	[JsonPropertyName("url")]
	public string URL { get; set; }
}

public class MinecraftVersionMeta
{
	[JsonPropertyName("arguments")]
	public MinecraftArguments Arguments { get; set; }
	[JsonPropertyName("assetIndex")]
	public MinecraftAssetIndexInfo AssetIndex { get; set; }
	[JsonPropertyName("assets")]
	public string Assets { get; set; }
	[JsonPropertyName("complianceLevel")]
	public int ComplianceLevel { get; set; }
	[JsonPropertyName("downloads")]
	public MinecraftDownloads Downloads { get; set; }
	[JsonPropertyName("id")]
	public string Id { get; set; }
	[JsonPropertyName("javaVersion")]
	public MinecraftJavaVersion JavaVersion { get; set; }
	[JsonPropertyName("libraries")]
	public List<MinecraftLibrary> Libraries { get; set; }
	[JsonPropertyName("mainClass")]
	public string MainClass { get; set; }
}

public class MinecraftArguments
{
	[JsonPropertyName("game")]
	public JsonArray Game { get; set; }
	[JsonPropertyName("jvm")]
	public JsonArray JVM { get; set; }
}

public class MinecraftDownloads
{
	[JsonPropertyName("client")]
	public MinecraftDownload Client { get; set; }
	[JsonPropertyName("client_mappings")]
	public MinecraftDownload ClientMappings { get; set; }
	[JsonPropertyName("server")]
	public MinecraftDownload Server { get; set; }
	[JsonPropertyName("server_mappings")]
	public MinecraftDownload ServerMappings { get; set; }
}

public class MinecraftDownload
{
	[JsonPropertyName("sha1")]
	public byte[] Sha1 { get; set; }
	[JsonPropertyName("size")]
	public long Size { get; set; }
	[JsonPropertyName("url")]
	public string Url { get; set; }
}

public class MinecraftJavaVersion
{
	[JsonPropertyName("component")]
	public string Component { get; set; }
	[JsonPropertyName("majorVersion")]
	public int MajorVersion { get; set; }
}

public class MinecraftLibrary
{
	[JsonPropertyName("downloads")]
	public MinecraftLibraryDownloads Downloads { get; set; }
	[JsonPropertyName("name")]
	public string Name { get; set; }
	[JsonPropertyName("natives")]
	public Dictionary<string, string> Natives { get; set; }
	[JsonPropertyName("rules")]
	public List<MinecraftRule> Rules { get; set; }
	[JsonPropertyName("extract.exclude")]
	public List<string> ExtractExclude { get; set; }
}

public class MinecraftLibraryDownloads
{
	[JsonPropertyName("artifact")]
	public MinecraftLibraryDownload Artifact { get; set; }
	[JsonPropertyName("classifiers")]
	public Dictionary<string, MinecraftLibraryDownload> Classifiers { get; set; }
}

public class MinecraftLibraryDownload
{
	[JsonPropertyName("path")]
	public string Path { get; set; }
	[JsonPropertyName("sha1")]
	public string Sha1 { get; set; }
	[JsonPropertyName("size")]
	public long Size { get; set; }
	[JsonPropertyName("url")]
	public string Url { get; set; }
}

public class MinecraftRule
{
	[JsonPropertyName("action")]
	// allow or disallow
	public string Action { get; set; }
	[JsonPropertyName("os")]
	public MinecraftRuleOS OS { get; set; }
	[JsonPropertyName("features")]
	public Dictionary<string, bool> Features { get; set; }


	public bool Allowed(string osName, string osVersion, string archName, List<string> features)
	{
		bool actionAllow = false;
		if(Action == "allow") actionAllow = true;
		else if(Action == "disallow") actionAllow = false;
		else throw new Exception("Rule action is not one of {allow, disallow}");

		return actionAllow == Matches(osName, osVersion, archName, features);
	}

	public bool Matches(string osName, string osVersion, string archName, List<string> features)
	{
		if(OS != null)
		{
			if(OS.Name != null && osName != OS.Name) return false;
			if(OS.Arch != null && archName != OS.Arch) return false;
			// TODO OS version check
		}

		if(Features != null)
			foreach(var f in Features)
				if(f.Value != features.Contains(f.Key)) return false;

		return true;
	}
}

public class MinecraftRuleOS
{
	[JsonPropertyName("name")]
	public string Name { get; set; }
	[JsonPropertyName("arch")]
	public string Arch { get; set; }
	[JsonPropertyName("version")]
	public string Version { get; set; }
}

public class MinecraftAssetIndex
{
	[JsonPropertyName("objects")]
	public Dictionary<string, MinecraftAsset> Objects { get; set; }
}
public class MinecraftAsset
{
	[JsonPropertyName("hash")]
	public string Hash { get; set; }
	[JsonPropertyName("size")]
	public long Size { get; set; }
}