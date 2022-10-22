using Godot;
using System;
using System.Linq;
using System.Text.Json;
using Godot.Collections;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

public partial class Main
{
	async Task<(int code, DeviceCodeResult result)> GetDeviceCode(string clientId, string scopes)
	{
		var res = await this.Post("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode",
			new string[] { "Content-Type: application/x-www-form-urlencoded" },
			$"client_id={clientId}&scope={scopes}");
		return (res.code, JsonSerializer.Deserialize<DeviceCodeResult>(res.result.GetStringFromUTF8()));
	}

	async Task<(int code, AuthenticationStatus result)> CheckAuthenticationStatus(string deviceCode, string clientId)
	{
		var res = await this.Get("https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
			new string[] { "Content-Type: application/x-www-form-urlencoded" },
			string.Join("&", new string[]
				{
					"grant_type=urn:ietf:params:oauth:grant-type:device_code",
					"client_id=" + clientId,
					"device_code=" + deviceCode
				}));

		return (res.code, JsonSerializer.Deserialize<AuthenticationStatus>(res.result.GetStringFromUTF8()));
	}

	async Task<(int code, XboxLogInResult result)> XboxLogIn(string token)
	{
		var reqJson = new Dictionary()
		{
			["Properties"] = new Godot.Collections.Dictionary()
			{
				["AuthMethod"] = "RPS",
				["SiteName"] = "user.auth.xboxlive.com",
				["RpsTicket"] = "d=" + token
			},
			["RelyingParty"] = "http://auth.xboxlive.com",
			["TokenType"] = "JWT"
		};

		var res = await this.Post("https://user.auth.xboxlive.com/user/authenticate",
			new string[] { "Content-Type: application/json", "Accept: application/json" },
			JSON.Stringify(reqJson));

		var loginresult = JsonSerializer.Deserialize<XboxLogInResult>(res.result.GetStringFromUTF8());

		var json = JSON.ParseString(res.result.GetStringFromUTF8()).AsGodotDictionary();
		loginresult.UserHash = (string)json
			["DisplayClaims"].AsGodotDictionary()
			["xui"].AsGodotArray()
			[0].AsGodotDictionary()
			["uhs"];

		return (res.code, loginresult);
	}

	async Task<(int code, string xsts)> GetMinecraftXSTS(string xboxtoken)
	{
		var reqJson = new Dictionary()
		{
			["Properties"] = new Godot.Collections.Dictionary()
			{
				["SandboxId"] = "RETAIL",
				["UserTokens"] = new Godot.Collections.Array()
				{
					xboxtoken
				}
			},
			["RelyingParty"] = "rp://api.minecraftservices.com/",
			["TokenType"] = "JWT"
		};

		var res = await this.Post(
			"https://xsts.auth.xboxlive.com/xsts/authorize",
			new string[] { "Content-Type: application/json", "Accept: application/json" },
			JSON.Stringify(reqJson));

		var json = JSON.ParseString(res.result.GetStringFromUTF8()).AsGodotDictionary();

		return (res.code, (string)json["Token"]);
	}

	async Task<(int code, MinecraftLogInResult result)> MinecraftLogIn(string userhash, string xsts)
	{
		var reqJson = new Dictionary()
		{
			["identityToken"] = $"XBL3.0 x={userhash};{xsts}"
		};

		var res = await this.Post(
			"https://api.minecraftservices.com/authentication/login_with_xbox",
			new string[] { "Content-Type: application/json", "Accept: application/json" },
			JSON.Stringify(reqJson));

		return (res.code, JsonSerializer.Deserialize<MinecraftLogInResult>(res.result.GetStringFromUTF8()));
	}

	async Task<(int code, MinecraftProfile result)> GetMinecraftProfile(string token)
	{
		var res = await this.Get("https://api.minecraftservices.com/minecraft/profile",
			new string[] {
				"Accept: application/json",
				"Authorization: Bearer " + token});

		return (res.code, JsonSerializer.Deserialize<MinecraftProfile>(res.result.GetStringFromUTF8()));
	}
}

public class DeviceCodeResult
{
	// Code the user has to enter on a website
	[JsonPropertyName("user_code")]
	public string Code { get; set; }

	// Website the user has to open and enter a code
	[JsonPropertyName("verification_uri")]
	public string AuthenticationURL { get; set; }

	// Code used for checking, if the user has authenticated
	[JsonPropertyName("device_code")]
	public string DeviceCode { get; set; }

	// Amount of seconds the user has to complete log in
	[JsonPropertyName("expires_in")]
	public int ExpiresInSeconds { get; set; }

	// Amount of seconds to wait before checking authentication status
	[JsonPropertyName("interval")]
	public int IntervalSec { get; set; }
}

public class AuthenticationStatus
{
	[JsonPropertyName("access_token")]
	public string AccessToken { get; set; }
	[JsonPropertyName("error")]
	public string Error { get; set; }
}

public class XboxLogInResult
{
	public string UserHash { get; set; }
	[JsonPropertyName("Token")]
	public string Token { get; set; }
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
	public System.Collections.Generic.List<MinecraftSkin> Skins {get;set;}
	[JsonPropertyName("capes")]
	public System.Collections.Generic.List<MinecraftCape> Capes {get;set;}
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