using Godot;
using Godot.Collections;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Text.Json;

public static class MSA
{
	public static async Task<DeviceCodeResult> GetDeviceCode(string clientId, string scopes)
	{
		return await new RequestBuilder("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode")
			.Post<DeviceCodeResult>(new System.Collections.Generic.Dictionary<string,string>()
			{
				["client_id"] = clientId,
				["scope"] = scopes
			});
	}

	public static async Task<AuthenticationStatus> CheckAuthenticationStatus(string deviceCode, string clientId)
	{
		return await new RequestBuilder("https://login.microsoftonline.com/consumers/oauth2/v2.0/token")
			.Post<AuthenticationStatus>(new System.Collections.Generic.Dictionary<string,string>()
			{
				["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
				["client_id"] = clientId,
				["device_code"] = deviceCode
			});
	}

	public static async Task<RefreshAccessTokenResponse> RefreshAccessToken(string refreshToken, string clientId, string scopes)
	{
		return await new RequestBuilder("https://login.microsoftonline.com/consumers/oauth2/v2.0/token")
			.Post<RefreshAccessTokenResponse>(new System.Collections.Generic.Dictionary<string,string>()
			{
				["grant_type"] = "refresh_token",
				["refresh_token"] = refreshToken,
				["client_id"] = clientId,
				["scopes"] = scopes
			});
	}

	public static async Task<XboxLogInResult> XboxLogIn(string token)
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

		var jstr = await new RequestBuilder("https://user.auth.xboxlive.com/user/authenticate")
			.Header("Accept","application/json")
			.PostJson<string>(JSON.Stringify(reqJson));

		var loginresult = JsonSerializer.Deserialize<XboxLogInResult>(jstr);

		var json = JSON.ParseString(jstr).AsGodotDictionary();
		loginresult.UserHash = (string)json
			["DisplayClaims"].AsGodotDictionary()
			["xui"].AsGodotArray()
			[0].AsGodotDictionary()
			["uhs"];

		return loginresult;
	}

	public static async Task<string> GetMinecraftXSTS(string xboxtoken)
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

		var jstr = await new RequestBuilder("https://xsts.auth.xboxlive.com/xsts/authorize")
			.Header("Accept","application/json")
			.PostJson<string>(JSON.Stringify(reqJson));

		var json = JSON.ParseString(jstr).AsGodotDictionary();

		return (string)json["Token"];
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
	[JsonPropertyName("refresh_token")]
	public string RefreshToken { get; set; }
	[JsonPropertyName("error")]
	public string Error { get; set; }
}

public class RefreshAccessTokenResponse
{
	[JsonPropertyName("access_token")]
	public string AccessToken { get; set; }
	[JsonPropertyName("expires_in")]
	public int ExporesIn { get; set; }
	[JsonPropertyName("refresh_token")]
	public string RefreshToken { get; set; }
}

public class XboxLogInResult
{
	public string UserHash { get; set; }
	[JsonPropertyName("Token")]
	public string Token { get; set; }
}