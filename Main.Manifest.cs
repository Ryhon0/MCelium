using Godot;
using System;
using System.Text.Json;
using Godot.Collections;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

public partial class Main
{
	[Export]
	OptionButton VersionOptions;

	public async Task<MinecraftManifest> GetMinecraftManifest()
	{
		var res = await this.Get("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json",
			new string[]{"Accept: application/json"});
		
		return JsonSerializer.Deserialize<MinecraftManifest>(res.result.GetStringFromUTF8());
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
	public string Id {get;set;}
	[JsonPropertyName("type")]
	public string VersionType {get;set;}
	[JsonPropertyName("url")]
	public string Url {get;set;}
	[JsonPropertyName("time")]
	public DateTime UpdatedAt {get;set;}
	[JsonPropertyName("releaseTime")]
	public DateTime ReleasedAt {get;set;}
	[JsonPropertyName("sha1")]
	public byte[] Sha1 {get;set;}
	[JsonPropertyName("complianceLevel")]
	public int ComplianceLevel {get;set;}
}
