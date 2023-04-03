using Godot;
using System.IO;
using Godot.Collections;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Text.Json;

public static class Minecraft
{
	public static async Task<MinecraftLogInResult> LogIn(string userhash, string xsts)
	{
		var reqJson = new Dictionary()
		{
			["identityToken"] = $"XBL3.0 x={userhash};{xsts}"
		};

		return await new RequestBuilder("https://api.minecraftservices.com/authentication/login_with_xbox")
			.Header("Accept","application/json")
			.PostJson<MinecraftLogInResult>(Json.Stringify(reqJson));
	}

	public static async Task<MinecraftProfile> GetProfile(string token)
	{
		return await new RequestBuilder("https://api.minecraftservices.com/minecraft/profile")
			.Header("Accept","application/json")
			.Header("Authorization", "Bearer " + token)
			.Get<MinecraftProfile>();
	}

	public static async Task ChangeSkin(string token, string variant, string url)
	{
		await new RequestBuilder("https://api.minecraftservices.com/minecraft/profile/skins")
			.Header("Authorization", "Bearer " + token)
			.PostJson<string>(JsonSerializer.Serialize(new {variant = variant, url = url}));
	}

	public static async Task ResetSkin(string token)
	{
		await new RequestBuilder("https://api.minecraftservices.com/minecraft/profile/skins/active")
			.Header("Authorization", "Bearer " + token)
			.Send<string>(HttpMethod.Delete, null, null);
	}

	public static async Task UploadSkin(string token, string variant, Stream data)
	{
		var mpf = new MultipartFormDataContent();
		
		mpf.Add(new StringContent(variant), "variant");
		var ds = new MemoryStream();
		mpf.Add(new ByteArrayContent(ds.GetBuffer()), "variant", "skin.png");
		
		var s = new MemoryStream();
		await mpf.CopyToAsync(s);

		await new RequestBuilder("https://api.minecraftservices.com/minecraft/profile/skins")
			.Header("Authorization", "Bearer " + token)
			.Post<string>(s.GetBuffer(), "multipart/form-data");
	}
}

public class MinecraftLogInResult
{
	[JsonPropertyName("username")]
	public string UUID { get; set; }
	[JsonPropertyName("access_token")]
	public string AccessToken { get; set; }
	[JsonPropertyName("token_type")]
	public string TokenType { get; set; }
	[JsonPropertyName("expires_in")]
	public int ExpiresInSeconds { get; set; }
}

public class MinecraftProfile
{
	[JsonPropertyName("id")]
	public string UUID { get; set; }
	[JsonPropertyName("name")]
	public string Username { get; set; }
	[JsonPropertyName("skins")]
	public System.Collections.Generic.List<MinecraftSkin> Skins { get; set; }
	[JsonPropertyName("capes")]
	public System.Collections.Generic.List<MinecraftCape> Capes { get; set; }
}

public class MinecraftSkin
{
	public const string VariantClassic = "CLASSIC";
	public const string VariantSlim = "SLIM";
	public const string StateActive = "ACTIVE";


	[JsonPropertyName("id")]
	public string Id { get; set; }
	[JsonPropertyName("state")]
	public string State { get; set; }
	[JsonPropertyName("url")]
	public string Url { get; set; }
	[JsonPropertyName("variant")]
	public string Variant { get; set; }
	[JsonPropertyName("alias")]
	public string Alias { get; set; }
}

public class MinecraftCape
{
	[JsonPropertyName("id")]
	public string Id { get; set; }
	[JsonPropertyName("url")]
	public string Url { get; set; }
	[JsonPropertyName("state")]
	public string State { get; set; }
	[JsonPropertyName("alias")]
	public string Alias { get; set; }
}