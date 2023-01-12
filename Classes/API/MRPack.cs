using System.Collections.Generic;
using System.Text.Json.Serialization;

public class MRPackIndex
{
	[JsonPropertyName("formatVersion")]
	/// The version of the format, stored as a number. The current value at the time of writing is `1`.
	public int FormatVersion { get; set; }
	[JsonPropertyName("game")]
	/// The game of the modpack, stored as a string. The only available type is `minecraft`.
	public string Game { get; set; }
	[JsonPropertyName("versionId")]
	/// A unique identifier for this specific version of the modpack.
	public string VersionID { get; set; }
	[JsonPropertyName("name")]
	/// Human-readable name of the modpack.
	public string Name { get; set; }
	[JsonPropertyName("summary")]
	/// A short description of this modpack.
	public string Summary { get; set; }
	[JsonPropertyName("files")]
	/// The files array contains a list of files for the modpack that needs to be downloaded
	public List<MRPackFile> Files { get; set; }
	[JsonPropertyName("dependencies")]
	/// This object contains a list of IDs and version numbers that launchers will use in order to know what to install.
	/// Available dependency IDs are:
	/// 
	/// minecraft - The Minecraft game
	/// forge - The Minecraft Forge mod loader
	/// fabric-loader - The Fabric loader
	/// quilt-loader - The Quilt loader
	public Dictionary<string, string> Dependencies { get; set; }
}

public class MRPackFile
{
	[JsonPropertyName("path")]
	/// The destination path of this file, relative to the Minecraft instance directory. For example, mods/MyMod.jar resolves to .minecraft/mods/MyMod.jar.
	public string Path {get;set;}
	[JsonPropertyName("hashes")]
	/// The hashes of the file specified.
	public ModrinthHashes Hashes {get;set;}
	[JsonPropertyName("env")]
	/// For files that only exist on a specific environment, this field allows that to be specified. It's an object which contains a `client` and `server` value. This uses the Modrinth client/server type specifications
	public Dictionary<string,string> Env {get;set;}
	[JsonPropertyName("downloads")]
	/// An array containing HTTPS URLs where this file may be downloaded
	public List<string> Downloads{get;set;}
	[JsonPropertyName("fileSize")]
	/// An integer containing the size of the file, in bytes. This is mostly provided as a utility for launchers to allow use of progress bars.
	public long FileSize {get;set;}
}