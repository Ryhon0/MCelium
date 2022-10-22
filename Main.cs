using Godot;
using System;
using Godot.Collections;
using System.Threading.Tasks;
using System.Linq;


// https://wiki.vg/Microsoft_Authentication_Scheme#Authenticate_with_Xbox_Live
// https://learn.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-device-code
// https://wiki.vg/Mojang_API
public partial class Main : Control
{
	[Export]
	Timer RetryTimer, AuthenticateTimer;
	[Export]
	Label URLLabel, CodeLabel, TextPageLabel, UsernameLabel;
	[Export]
	TabContainer Tabs;
	[Export]
	Control TextPage, AuthenticationPage, LoggedInPage, AuthenticatePopup;
	[Export]
	Node3D PlayerModel;

	string AuthPageURL;

	const string OAuthScopes = "XboxLive.signin offline_access";
	string Clientid = "b26b832c-d001-4dd0-943c-fcc3fd812964";
	public override async void _Ready()
	{
		AuthenticatePopup.Hide();

		var manifest = await GetMinecraftManifest();
		VersionOptions.AddItem("* Release " + manifest.Latest.Release);
		VersionOptions.AddItem("* Snapshot " + manifest.Latest.Snapshot);
		foreach (var v in manifest.Versions)
		{
			if (v.Id == manifest.Latest.Release ||
				v.Id == manifest.Latest.Snapshot)
				continue;

			var dic = new Dictionary<string, string>()
			{
				[MinecraftVersion.VersionTypeRelease] = "Release",
				[MinecraftVersion.VersionTypeSnapshot] = "Snapshot",
				[MinecraftVersion.VersionTypeAlpha] = "Alpha",
				[MinecraftVersion.VersionTypeBeta] = "Beta",
			};

			string vertype = v.VersionType;

			if (dic.ContainsKey(v.VersionType))
				vertype = dic[v.VersionType];

			VersionOptions.AddItem(vertype + " " + v.Id);
		}

		var version = manifest.Versions[0];
		var meta = await this.Get(version.Url, new string[] { "Accept: application/json" });

		var metajson = JSON.ParseString(meta.result.GetStringFromUTF8());

		Array<string> jvmargs = new() { "java" };
		Array<string> gameargs = new();

		bool isDemo = true;
		foreach (var arg in metajson.AsGodotDictionary()["arguments"].AsGodotDictionary()["game"].AsGodotArray())
		{
			if (arg.VariantType == Variant.Type.String) gameargs.Add((string)arg);
			else if (arg.VariantType == Variant.Type.Dictionary)
			{
				var dict = arg.AsGodotDictionary();

				void AddArgs()
				{
					if (dict["value"].VariantType == Variant.Type.String)
					{
						gameargs.Add((string)dict["value"]);
					}
					else if (dict["value"].VariantType == Variant.Type.Array)
					{
						foreach (var a in dict["value"].AsStringArray())
							gameargs.Add(a);
					}
				}

				if (FeaturesMatches(dict)) AddArgs();
			}
		}

		foreach (var arg in metajson.AsGodotDictionary()["arguments"].AsGodotDictionary()["jvm"].AsGodotArray())
		{
			if (arg.VariantType == Variant.Type.String) jvmargs.Add((string)arg);
			else if (arg.VariantType == Variant.Type.Dictionary)
			{
				var dict = arg.AsGodotDictionary();

				void AddArgs()
				{
					if (dict["value"].VariantType == Variant.Type.String)
					{
						jvmargs.Add((string)dict["value"]);
					}
					else if (dict["value"].VariantType == Variant.Type.Array)
					{
						foreach (var a in dict["value"].AsStringArray())
							jvmargs.Add(a);
					}
				}

				if (FeaturesMatches(dict)) AddArgs();
			}
		}

		Array<string> newargs = new();
		foreach (var a in gameargs)
		{
			System.Collections.Generic.Dictionary<string, string> lookup = new()
			{
				["auth_player_name"] = "Ryhon_",
				["version_name"] = version.Id,
				["game_directory"] = "/tmp/game",
				["assets_root"] = "/tmp/assets",
				["assets_index_name"] = "/tmp/assets/index",
				["auth_uuid"] = "0123456789abcdef",
				["auth_access_token"] = "0123456789abcdef",
				["clientid"] = "0123456789abcdef",
				["auth_xuid"] = "0123456789abcdef",
				["user_type"] = "normal_user",
				["version_type"] = version.VersionType,
			};

			// Why is there no equivalent to std.string : translate smh
			string arg = a;
			foreach (var k in lookup.Keys)
				arg = arg.Replace("${" + k + "}", lookup[k]);

			newargs.Add(arg);
		}
		gameargs = newargs;

		newargs = new();
		foreach (var a in jvmargs)
		{
			System.Collections.Generic.Dictionary<string, string> lookup = new()
			{
				["natives_directory"] = "/tmp/libs/",
				["launcher_name"] = "MCelium",
				["launcher_version"] = "0.0.1"
			};

			string arg = a;
			foreach (var k in lookup.Keys)
				arg = arg.Replace("${" + k + "}", lookup[k]);

			newargs.Add(arg);
		}
		jvmargs = newargs;


		GD.Print(jvmargs);
		GD.Print(gameargs);
	}

	async void Authenticate()
	{
		AuthenticatePopup.Show();

		Tabs.CurrentTab = TextPage.GetIndex();
		TextPageLabel.Text = "Requesting code...";

		#region Request device code
		var devicecode = await GetDeviceCode(Clientid, OAuthScopes);

		AuthPageURL = devicecode.result.AuthenticationURL;

		var prettyurl = AuthPageURL["https://www.".Length..];
		URLLabel.Text = string.Format(URLLabel.Text, prettyurl);
		CodeLabel.Text = devicecode.result.Code;
		AuthenticateTimer.Start(devicecode.result.ExpiresInSeconds);
		#endregion

		#region Check if authenticated
		Tabs.CurrentTab = AuthenticationPage.GetIndex();

		RetryTimer.OneShot = true;
		RetryTimer.Start(devicecode.result.IntervalSec);

		string accessToken = null;
		while (accessToken == null)
		{
			while (RetryTimer.TimeLeft != 0)
				await ToSignal(RetryTimer, "timeout");

			var authStatus = await CheckAuthenticationStatus(devicecode.result.DeviceCode, Clientid);

			if (authStatus.code != 200)
			{
				GD.Print(authStatus.code);
				RetryTimer.Start(devicecode.result.IntervalSec);
				continue;
			}

			accessToken = authStatus.result.AccessToken;
			break;
		}
		#endregion

		#region Log in to XBL

		TextPageLabel.Text = "Logging in to Xbox Live...";
		Tabs.CurrentTab = TextPage.GetIndex();

		var xbox = await XboxLogIn(accessToken);

		if (xbox.code != 200)
		{
			GD.Print("Failed: " + xbox.code);
			GD.Print(xbox.result);
			return;
		}
		#endregion

		#region Minecraft log in
		TextPageLabel.Text = "Logging in to Minecraft...";
		Tabs.CurrentTab = TextPage.GetIndex();

		var mcxstsres = await GetMinecraftXSTS(xbox.result.Token);
		var mcxsts = mcxstsres.xsts;

		var mclogin = await MinecraftLogIn(xbox.result.UserHash, mcxsts);

		#endregion

		#region Minecraft profile
		TextPageLabel.Text = "Obtaining Minecraft profile...";
		Tabs.CurrentTab = TextPage.GetIndex();

		var profile = await GetMinecraftProfile(mclogin.result.AccessToken);

		GD.Print(profile.result.Username);
		#endregion

		#region Player skin
		var skin = profile.result.Skins.FirstOrDefault(s => s.State == MinecraftSkin.StateActive);

		if (skin != null)
		{

			if (skin.Variant == MinecraftSkin.VariantSlim)
			{
				PlayerModel.GetNode<Node3D>("ClassicOverlay").Visible = false;
				PlayerModel.GetNode<Node3D>("Classic").Visible = false;
			}
			else
			{
				PlayerModel.GetNode<Node3D>("SlimOverlay").Visible = false;
				PlayerModel.GetNode<Node3D>("Slim").Visible = false;
			}
			var png = (await this.Get(skin.Url, new string[] { "Accept: image/png" })).result;
			var img = new Image();
			img.LoadPngFromBuffer(png);

			var tex = ImageTexture.CreateFromImage(img);
			(PlayerModel.GetChild<MeshInstance3D>(0)
				.Mesh.SurfaceGetMaterial(0) as StandardMaterial3D)
				.AlbedoTexture = tex;
		}
		#endregion

		Tabs.CurrentTab = LoggedInPage.GetIndex();
		UsernameLabel.Text = profile.result.Username;
	}

	async void Play()
	{
		var manifest = await GetMinecraftManifest();
		var version = manifest.Versions[0];

		var meta = JSON.ParseString(
			(await this.Get(version.Url, new string[] { "Accept: application/json" }))
			.result.GetStringFromUTF8()).AsGodotDictionary();

		// Download assets
		{
			var assetIndex = meta["assetIndex"].AsGodotDictionary();

			var objects = JSON.ParseString(
				(await this.Get((string)assetIndex["url"], new string[] { "Accept: application/json" }))
				.result.GetStringFromUTF8()).AsGodotDictionary()
				["objects"].AsGodotDictionary();

			GD.Print("Downloading " + objects.Count + " objects...");
			// https://wiki.vg/Game_files
			foreach (var obj in objects)
			{
				var objd = obj.Value.AsGodotDictionary();
				var hash = (string)objd["hash"];
				string url = "http://resources.download.minecraft.net/" + hash[0..2] + "/" + hash;

				var outpath = ".minecraft/assets/objects/" + hash[0..2] + "/" + hash;


				if (!System.IO.File.Exists(outpath))
				{
					GD.Print(url + " => " + outpath);
					var fi = new System.IO.FileInfo(outpath);
					System.IO.Directory.CreateDirectory(fi.DirectoryName);

					var res = await this.Get(url, new string[] { });
					await System.IO.File.WriteAllBytesAsync(outpath, res.result);
				}
			}

			GD.Print("Done!");
		}

		// Download client jar
		{
			GD.Print("Downloading client.jar");

			var url = (string)meta["downloads"].AsGodotDictionary()["client"].AsGodotDictionary()["url"];

			var outpath = ".minecraft/client.jar";
			if (!System.IO.File.Exists(outpath))
			{
				var fi = new System.IO.FileInfo(outpath);
				System.IO.Directory.CreateDirectory(fi.DirectoryName);

				var jar = (await this.Get(url, new string[] { })).result;
				await System.IO.File.WriteAllBytesAsync(outpath, jar);
			}

			GD.Print("Done!");
		}

		// Download libraries
		{
			GD.Print("Downloading libraries");
			var libs = meta["libraries"].AsGodotArray();

			foreach (var lib in libs)
			{
				var artifact = lib.AsGodotDictionary()["downloads"].AsGodotDictionary()["artifact"].AsGodotDictionary();

				var outpath = ".minecraft/libs/" + (string)artifact["path"];
				var dlurl = (string)artifact["url"];

				if(!System.IO.File.Exists(outpath))
				{
					GD.Print(dlurl + " => " + outpath);

					var fi = new System.IO.FileInfo(outpath);
					System.IO.Directory.CreateDirectory(fi.DirectoryName);

					var dat = (await this.Get(dlurl, new string[]{})).result;
					await System.IO.File.WriteAllBytesAsync(outpath, dat);
				}
			}
			GD.Print("Done!");
		}

		// Download Java
		{
			var javaVersion = (int)meta["javaVersion"].AsGodotDictionary()["majorVersion"];
			string os = "linux";
			string arch = "x64";
			var javaurl = $"https://api.adoptium.net/v3/assets/latest/{javaVersion}/hotspot?os={os}&architecture={arch}&image_type=jre";

			GD.Print(javaurl);
			var adoptiumjson = JSON.ParseString((await this.Get(javaurl, new string[] { })).result.GetStringFromUTF8()).AsGodotArray()[0].AsGodotDictionary();
			var dlurl = (string)adoptiumjson["binary"].AsGodotDictionary()["package"].AsGodotDictionary()["link"];
			GD.Print(dlurl);

			// var tar = (await this.Get(dlurl, new string[] { })).result;
		}
	}

	void OpenAuthPage()
	{
		OS.ShellOpen(AuthPageURL);
	}

	bool FeaturesMatches(Dictionary dict)
	{
		bool isDemo = false;
		bool hasCustomResolution = false;

		var rules = dict["rules"].AsGodotArray();
		foreach (var r in rules)
		{
			var rule = r.AsGodotDictionary();
			// Haven't seen it be any other option, ignore if that's the case
			if (dict.ContainsKey("action") && (string)dict["action"] != "allow") continue;

			if (rule.ContainsKey("features"))
			{
				var features = rule["features"].AsGodotDictionary();

				foreach (var f in features)
				{
					switch ((string)f.Key)
					{
						case "is_demo_user":
							if (isDemo != (bool)f.Value) return false;
							break;
						case "has_custom_resolution":
							if (hasCustomResolution != (bool)f.Value) return false;
							break;
						// Unknown feature, skip argument
						default:
							return false;
					}
				}
			}

			if (rule.ContainsKey("os"))
			{
				var os = rule["os"].AsGodotDictionary();

				string osname = null;
				if (OperatingSystem.IsWindows()) osname = "windows";
				else if (OperatingSystem.IsLinux()) osname = "linux";
				else if (OperatingSystem.IsMacOS()) osname = "osx";

				var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLower();

				if (os.ContainsKey("name"))
				{
					if ((string)os["name"] != osname) return false;
					else
					{
						if (os.ContainsKey("version"))
						{
							// ^10\. - Win 10 or higher
						}
					}
				}

				// No idea if it checks the bitness or just architecture
				if (os.ContainsKey("arch"))
					if ((string)os["arch"] != arch) return false;
			}
		}

		return true;
	}
}
