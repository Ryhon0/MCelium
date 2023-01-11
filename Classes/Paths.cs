using Godot;
using System.IO;

public static class Paths
{
	public static string User => OS.GetUserDataDir();

	public static string WebCache => User + "/webcache";
	public static string Profiles => User + "/profiles.json";
	public static string Instances => User + "/instances";
	public static string Assets => User + "/assets";
	public static string AssetsObjects => Assets + "/objects";
	public static string AssetsIndexes => Assets + "/indexes";
	public static string Java => User + "/java";
}