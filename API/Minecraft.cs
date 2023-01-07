using Godot;
using Godot.Collections;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

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
			.PostJson<MinecraftLogInResult>(JSON.Stringify(reqJson));
	}

	public static async Task<MinecraftProfile> GetProfile(string token)
	{
		return await new RequestBuilder("https://api.minecraftservices.com/minecraft/profile")
			.Header("Accept","application/json")
			.Header("Authorization", "Bearer " + token)
			.Get<MinecraftProfile>();
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


	[JsonPropertyName("name")]
	public string Username { get; set; }
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