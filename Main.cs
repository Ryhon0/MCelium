using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Collections.Generic;
using SharpCompress.Archives.Zip;
using System.Diagnostics;


// https://wiki.vg/Microsoft_Authentication_Scheme#Authenticate_with_Xbox_Live
// https://learn.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-device-code
// https://wiki.vg/Mojang_API
public partial class Main : Control
{
	const string OAuthScopes = "XboxLive.signin offline_access";
	string Clientid = "b26b832c-d001-4dd0-943c-fcc3fd812964";

	[Export]
	SkinViewer SkinViewer;
	[Export]
	ItemList InstanceList;
	[Export]
	Control InstanceProperties;
	[Export]
	Label InstanceNameLabel;

	[Export]
	PackedScene MSAPopupScene, NewInstanceScene, ModsScene;

	[Export]
	Texture2D NewInstanceIcon;
	[Export]
	Texture2D DefaultInstanceIcon;

	List<Instance> Instances = new();
	List<Profile> Profiles;

	public override async void _Ready()
	{
		InstanceProperties.Hide();
		InstanceList.Clear();

		Java.Versions = await Java.LoadVersions();
		Instances = await Instance.LoadInstances();

		foreach (var i in Instances)
		{
			var id = InstanceList.AddIconItem(DefaultInstanceIcon);
			InstanceList.SetItemText(id, i.Name);
		}
		{
			var id = InstanceList.AddIconItem(NewInstanceIcon);
			InstanceList.SetItemText(id, "New instance");
		}
		InstanceList.GrabFocus();

		if (File.Exists(Paths.Profiles))
		{
			var j = await File.ReadAllTextAsync(Paths.Profiles);
			Profiles = System.Text.Json.JsonSerializer.Deserialize<List<Profile>>(j);
		}
		else
		{
			var p = MSAPopupScene.Instantiate<MSAPopup>();
			AddChild(p);
			p.Authenticate();
		}
	}

	void OnInstanceSelected(int idx)
	{
		var isInstance = idx != InstanceList.ItemCount - 1;
		InstanceProperties.Visible = isInstance;
		if (!isInstance) return;

		var i = Instances[idx];

		InstanceNameLabel.Text = i.Name;
	}

	void PlaySelected()
	{
		var i = Instances[InstanceList.GetSelectedItems()[0]];
		var p = Profiles.First();

		LaunchInstance(i, p);
	}

	void ShowModsScreen()
	{
		var i = Instances[InstanceList.GetSelectedItems()[0]];

		var m = ModsScene.Instantiate<Modding>();
		m.Instance = i;
		AddChild(m);
	}

	void UninstallSelected()
	{
		var id = InstanceList.GetSelectedItems()[0];
		var i = Instances[id];
		i.Uninstall();

		InstanceList.RemoveItem(id);
		Instances.Remove(i);
	}

	void OnInstanceActivated(int idx)
	{
		if (idx == InstanceList.ItemCount - 1)
		{
			GD.Print("Creating new instance...");
			var i = NewInstanceScene.Instantiate<NewInstance>();
			AddChild(i);
		}
		else
		{
			var i = Instances[idx];
			GD.Print("Launching " + i.Name + "...");

			var p = Profiles.First();

			LaunchInstance(i, p);
		}
	}

	async void LaunchInstance(Instance i, Profile p)
	{
		var mainClass = i.Meta.MainClass;

		if(i.Fabric != null)
		{
			if (i.Fabric.LauncherMeta.MainClass is JsonObject o)
				mainClass = (string)(o["client"]);
			else mainClass = (string)i.Fabric.LauncherMeta.MainClass;
		}

		string osName = "linux";
		string osVersion = null;
		string archName = "x86";
		List<string> features = new();

		var java = Java.Versions.First(j => j.MajorVersion == i.Meta.JavaVersion.MajorVersion).GetExecutable();

		List<string> args = new() { "-Xms512m", "-Xmx4096m", "-Duser.language=en" };

		IEnumerable<string> ProcessArguments(IEnumerable<JsonNode> nodes)
		{
			foreach (var n in nodes)
			{
				if (n is JsonObject o)
				{
					var rules = JsonSerializer.Deserialize<List<MinecraftRule>>(o["rules"]);
					var allowed = !rules.Select(r => r.Allowed(osName, osVersion, archName, features)).Any(rr => !rr);

					var vo = o["value"];
					if (allowed)
						if (vo is JsonArray a)
						{
							foreach (var ao in a)
								yield return (string)ao;
						}
						else yield return (string)vo;
				}
				else yield return (string)n;
			}
		}

		var mcdir = i.GetMinecraftDirectory();
		Dictionary<string, string> replacekeys = new()
		{
			["auth_player_name"] = p.MCProfile.Username,
			["version_name"] = i.Version.Id,
			["game_directory"] = mcdir,
			["assets_root"] = Paths.Assets,
			["assets_index_name"] = i.Meta.AssetIndex.Id,
			["auth_uuid"] = p.MCProfile.UUID,
			["auth_access_token"] = p.MCToken, // Probably refresh it
			["clientid"] = "minecraft",
			["auth_xuid"] = p.Xuid,
			["user_type"] = "microsoft",
			["version_type"] = i.Version.VersionType,
			["natives_directory"] = mcdir + "/natives",
			["launcher_name"] = "MCelium",
			["launcher_version"] = "0.0.1",
			["classpath"] = String.Join(':', System.IO.Directory.GetFileSystemEntries(mcdir, "*.jar", SearchOption.AllDirectories))
		};

		if (i.Meta.Arguments != null)
		{
			args.AddRange(ProcessArguments(i.Meta.Arguments.JVM).ToList());
			args.Add(mainClass);
			args.AddRange(ProcessArguments(i.Meta.Arguments.Game).ToList());
		}
		else
		{
			args.Add("-Djava.library.path=${natives_directory}");
			args.Add("-cp");
			args.Add("${classpath}");
			args.Add(mainClass);
			args.AddRange(i.Meta.MinecraftArguments.Split(' '));
		}

		string ReplaceDict(string s, Dictionary<string, string> d)
		{
			string ss = s;
			foreach (var ds in d)
				ss = ss.Replace("${" + ds.Key + "}", ds.Value);

			return ss;
		}

		for (int ii = 0; ii < args.Count; ii++)
			args[ii] = ReplaceDict(args[ii], replacekeys);

		var psi = new ProcessStartInfo()
		{
			FileName = java,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			WorkingDirectory = mcdir
		};

		for (int ii = 0; ii < args.Count; ii++)
			psi.ArgumentList.Add(args[ii]);

		var proc = new Process();
		proc.StartInfo = psi;

		GD.Print("Starting...");

		proc.Start();
		await proc.WaitForExitAsync();

		GD.Print(proc.ExitCode);
		GD.Print(proc.StandardError.ReadToEnd());
	}
}
