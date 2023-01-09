using System;
using System.Collections.Generic;

public class Instance
{
	public static string InstanceFile = "instance.json";

	public string Name { get; set; }
	public string Id { get; set; }

	public MinecraftVersion Version { get; set; }
	public MinecraftVersionMeta Meta { get; set; }
	public InstanceFabricInfo Fabric { get; set; }
}

public class InstanceFabricInfo
{
	public Version Version { get; set; }
	/// List of maven libraries in the following format: com.example:package:1.0.0
	public List<string> Libraries { get; set; }
}