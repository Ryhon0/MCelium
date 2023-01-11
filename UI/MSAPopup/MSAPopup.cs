using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public partial class MSAPopup : VBoxContainer
{
	[Export]
	Timer RetryTimer, AuthenticateTimer;
	[Export]
	Label URLLabel, CodeLabel, TextPageLabel, UsernameLabel;

	[Export]
	TabContainer Tabs;
	[Export]
	Control TextPage, AuthenticationPage, LoggedInPage;

	[Export]
	SkinViewer SkinViewer;

	const string OAuthScopes = "XboxLive.signin offline_access";
	string Clientid = "b26b832c-d001-4dd0-943c-fcc3fd812964";

	string AuthPageURL;

	public async void Authenticate()
	{
		Show();

		Tabs.CurrentTab = TextPage.GetIndex();
		TextPageLabel.Text = "Requesting code...";

		#region Request device code
		var devicecode = await MSA.GetDeviceCode(Clientid, OAuthScopes);

		AuthPageURL = devicecode.AuthenticationURL;

		var prettyurl = AuthPageURL["https://www.".Length..];
		URLLabel.Text = string.Format(URLLabel.Text, prettyurl);
		CodeLabel.Text = devicecode.Code;
		AuthenticateTimer.Start(devicecode.ExpiresInSeconds);
		#endregion

		#region Check if authenticated
		Tabs.CurrentTab = AuthenticationPage.GetIndex();

		RetryTimer.OneShot = true;
		RetryTimer.Start(devicecode.IntervalSec);

		AuthenticationStatus authStatus = null;
		while (authStatus == null || authStatus.AccessToken == null)
		{
			while (RetryTimer.TimeLeft != 0)
				await ToSignal(RetryTimer, "timeout");

			try
			{
				authStatus = await MSA.CheckAuthenticationStatus(devicecode.DeviceCode, Clientid);
				if(authStatus.AccessToken == null)
					RetryTimer.Start(devicecode.IntervalSec);
			}
			catch (Exception ex)
			{
				RetryTimer.Start(devicecode.IntervalSec);
				continue;
			}
		}
		#endregion

		#region Log in to XBL

		TextPageLabel.Text = "Logging in to Xbox Live...";
		Tabs.CurrentTab = TextPage.GetIndex();

		var xbox = await MSA.XboxLogIn(authStatus.AccessToken);
		#endregion

		#region Minecraft log in
		TextPageLabel.Text = "Logging in to Minecraft...";
		Tabs.CurrentTab = TextPage.GetIndex();

		var mcxsts = await MSA.GetMinecraftXSTS(xbox.Token);

		var mclogin = await Minecraft.LogIn(xbox.UserHash, mcxsts);

		#endregion

		#region Minecraft profile
		TextPageLabel.Text = "Obtaining Minecraft profile...";
		Tabs.CurrentTab = TextPage.GetIndex();

		var profile = await Minecraft.GetProfile(mclogin.AccessToken);
		#endregion

		#region Player skin
		var skin = profile.Skins.FirstOrDefault(s => s.State == MinecraftSkin.StateActive);

		SkinViewer.ShowSkin(skin);
		#endregion

		Tabs.CurrentTab = LoggedInPage.GetIndex();
		UsernameLabel.Text = profile.Username;

		var p = new Profile()
		{
			MCProfile = profile,
			MSARefreshToken = authStatus.RefreshToken,
			Xuid = xbox.UserHash,
			MCToken = mclogin.AccessToken,
			MCTokenExpiresIn = DateTime.Now.AddSeconds(mclogin.ExpiresInSeconds)
		};

		var pj = System.Text.Json.JsonSerializer.Serialize(new List<Profile>(){p});
		await File.WriteAllTextAsync(Paths.Profiles, pj);
	}

	void OpenAuthPage()
	{
		OS.ShellOpen(AuthPageURL);
	}
}