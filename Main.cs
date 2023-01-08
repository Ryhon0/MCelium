using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Collections.Generic;
using SharpCompress.Archives.Zip;


// https://wiki.vg/Microsoft_Authentication_Scheme#Authenticate_with_Xbox_Live
// https://learn.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-device-code
// https://wiki.vg/Mojang_API
public partial class Main : Control
{
	const string ProfilePath = "user://profile.json";
	const string OAuthScopes = "XboxLive.signin offline_access";
	string Clientid = "b26b832c-d001-4dd0-943c-fcc3fd812964";

	[Export]
	SkinViewer SkinViewer;

	[Export]
	PackedScene MSAPopupScene;

	public override async void _Ready()
	{
		// AuthenticatePopup.Hide();

		if (false)
		{
			var s = await Modrinth.Search("inventory profiles", new (string, string)[] { ("versions", "1.19.2") });

			var mod = s.Hits.First();
			GD.Print($"{mod.Title} - https://modrinth.com/{mod.ProjectType}/{mod.Slug}");

			var vers = await Modrinth.GetVersions(mod.ProjectID);
			var v = vers.First(v => v.GameVersions.Contains("1.19.2")&&v.Loaders.Contains("fabric"));

			var f = v.Files.First(f => f.Primary);
			GD.Print("Download " + f.Url);

			foreach (var d in v.Dependencies.Where(d => d.DependencyType == "required"))
			{
				if (d.VersionId != null)
				{
					var dver = await Modrinth.GetVersion(d.VersionId);

					var df = dver.Files.First(f => f.Primary);
					GD.Print("Download " + df.Url);
				}
				else
				{
					var dvers = await Modrinth.GetVersions(d.ProjectId);
					var dver = dvers.First(dv=>dv.GameVersions.Contains("1.19.2")&&dv.Loaders.Contains("fabric"));

					var df = dver.Files.First(f => f.Primary);
					GD.Print("Download " + df.Url);
				}
			}
		}

		if (FileAccess.FileExists(ProfilePath))
		{
			var f = FileAccess.Open(ProfilePath, FileAccess.ModeFlags.ReadWrite);
			var j = f.GetAsText();
			var p = System.Text.Json.JsonSerializer.Deserialize<MCLProfile>(j);

			var rtr = await MSA.RefreshAccessToken(p.MSARefreshToken, Clientid, OAuthScopes);
			p.MSARefreshToken = rtr.RefreshToken;

			f.StoreString(JsonSerializer.Serialize(p));
			f = null;

			var xb = await MSA.XboxLogIn(rtr.AccessToken);
			var mcxsts = await MSA.GetMinecraftXSTS(xb.Token);
			var mcl = await Minecraft.LogIn(xb.UserHash, mcxsts);

			/*
			var mcp = await Minecraft.GetProfile(mcl.AccessToken);
			SkinViewer.ShowSkin(mcp.Skins.FirstOrDefault(s => s.State == MinecraftSkin.StateActive));
			GD.Print(mcp.Username);
			*/
		
			Play(mcl.AccessToken, mcl.UUID, xb.UserHash);
		}
		else
		{
			var p = MSAPopupScene.Instantiate<MSAPopup>();
			AddChild(p);
			p.Authenticate();
		}
	}
}
