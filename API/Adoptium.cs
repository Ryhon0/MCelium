using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public static class Adoptium
{
	public static async Task<List<AdoptiumResult>> GetJRE(string os, string arch, int major)
	{
		return await new RequestBuilder($"https://api.adoptium.net/v3/assets/latest/{major}/hotspot?os={os}&architecture={arch}&image_type=jre")
			.Get<List<AdoptiumResult>>();
	}
}

public class AdoptiumResult
{
	[JsonPropertyName("binary")]
	public AdoptiumBinary Binary {get;set;}
	[JsonPropertyName("release_name")]
	public string ReleaseName {get;set;}
}

public class AdoptiumBinary
{
	[JsonPropertyName("package")]
	public AdoptiumPackage Package {get;set;}
	[JsonPropertyName("project")]
	public string Project {get;set;}
}

public class AdoptiumPackage
{
	[JsonPropertyName("checksum")]
	public byte[] Checksum {get;set;}
	[JsonPropertyName("link")]
	public string Link {get;set;}
	[JsonPropertyName("size")]
	public long Size {get;set;}
}
