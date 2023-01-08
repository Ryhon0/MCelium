using System;
using System.Net;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public static class Modrinth
{
	static string RootUrl = "https://api.modrinth.com/v2";

	public static async Task<ModrinthSearchResponse> Search
		(string query, (string, string)[] facets, string index = "relevance", int offset = 0, int limit = 10, string filters = null)
	{
		return await new RequestBuilder(RootUrl + "/search")
			.Query("query", query)
			.Query("facets", // This is awful and will break eventually 🙃
				"[" +
				String.Join(',',
				facets.Select(f => "[\"" + f.Item1 + ":" + f.Item2 + "\"]"))
				+ "]")
			.Query("index", index)
			.Query("offset", offset)
			.Query("limit", limit)
			.Query("filters", filters)
			.Get<ModrinthSearchResponse>();
	}

	public static async Task<List<ModrinthModVersion>> GetVersions(string id)
	{
		return await new RequestBuilder(RootUrl + $"/project/{id}/version")
			.Get<List<ModrinthModVersion>>();
	}

	public static async Task<ModrinthModVersion> GetVersion(string id)
	{
		return await new RequestBuilder(RootUrl + $"/version/{id}")
			.Get<ModrinthModVersion>();
	}
}

public class ModrinthSearchResponse
{
	[JsonPropertyName("hits")]
	/// Array of objects
	/// The list of results
	public List<ModrinthSearchHit> Hits { get; set; }

	[JsonPropertyName("offset")]
	/// The number of results that were skipped by the query
	public int Offset { get; set; }

	[JsonPropertyName("limit")]
	/// The number of results that were returned by the query
	public int Limit { get; set; }

	[JsonPropertyName("total_hits")]
	/// The total number of results that match the query
	public int TotalHits { get; set; }
}

public class ModrinthSearchHit
{
	[JsonPropertyName("slug")]
	/// The slug of a project, used for vanity URLs. Regex: ^[\w!@$()`.+,"\-']{3,64}$
	public string Slug { get; set; }

	[JsonPropertyName("title")]
	/// The title or name of the project
	public string Title { get; set; }

	[JsonPropertyName("description")]
	/// A short description of the project
	public string Description { get; set; }

	[JsonPropertyName("categories")]
	/// A list of the categories that the project has
	public List<string> Categories { get; set; }

	[JsonPropertyName("client_side")]
	/// The client side support of the project
	/// One of required, optional, unsupported
	public string ClientSide { get; set; }

	[JsonPropertyName("server_side")]
	/// The server side support of the project
	/// One of required, optional, unsupported
	public string ServerSide { get; set; }

	[JsonPropertyName("project_type")]
	/// The project type of the project
	/// One of mod, modpack, resourcepack
	public string ProjectType { get; set; }

	[JsonPropertyName("downloads")]
	/// The total number of downloads of the project
	public int Downloads { get; set; }

	[JsonPropertyName("icon_url")]
	/// The URL of the project's icon
	public string IconUrl { get; set; }

	[JsonPropertyName("project_id")]
	/// The ID of the project
	public string ProjectID { get; set; }

	[JsonPropertyName("author")]
	/// The username of the project's author
	public string Author { get; set; }

	[JsonPropertyName("display_categories")]
	/// A list of the categories that the project has which are not secondary
	public List<string> DisplayCategories { get; set; }

	[JsonPropertyName("versions")]
	/// A list of the minecraft versions supported by the project
	public List<string> Versions { get; set; }

	[JsonPropertyName("follows")]
	/// The total number of users following the project
	public int Follows { get; set; }

	[JsonPropertyName("date_created")]
	/// The date the project was added to search
	public DateTime DateCreated { get; set; }

	[JsonPropertyName("date_modified")]
	/// The date the project was last modified
	public DateTime DateModified { get; set; }

	[JsonPropertyName("latest_version")]
	/// The latest version of minecraft that this project supports
	public string LatestVersion { get; set; }

	[JsonPropertyName("license")]
	/// The license of the project
	public string License { get; set; }

	[JsonPropertyName("gallery")]
	/// All gallery images attached to the project
	public List<string> Gallery { get; set; }
}

public class ModrinthModVersion
{
	[JsonPropertyName("name")]
	/// The name of this version
	public string Name { get; set; }

	[JsonPropertyName("version_number")]
	/// The version number. Ideally will follow semantic versioning
	public string VersionNumber { get; set; }

	[JsonPropertyName("changelog")]
	/// The changelog for this version
	public string Changelog { get; set; }

	[JsonPropertyName("dependencies")]
	/// A list of specific versions of projects that this version depends on
	public List<ModrinthDependency> Dependencies { get; set; }

	[JsonPropertyName("game_versions")]
	/// A list of versions of Minecraft that this version supports
	public List<string> GameVersions { get; set; }

	[JsonPropertyName("version_type")]
	/// The release channel for this version
	/// One of release, beta, alpha
	public string VersionType { get; set; }

	[JsonPropertyName("loaders")]
	/// The mod loaders that this version supports
	public List<string> Loaders { get; set; }

	[JsonPropertyName("featured")]
	/// Whether the version is featured or not
	public bool Featured { get; set; }

	[JsonPropertyName("id")]
	/// The ID of the version, encoded as a base62 string
	public string Id { get; set; }

	[JsonPropertyName("project_id")]
	/// The ID of the project this version is for
	public string ProjectId { get; set; }

	[JsonPropertyName("author_id")]
	/// The ID of the author who published this version
	public string AuthorId { get; set; }

	[JsonPropertyName("date_published")]
	public DateTime DatePublished { get; set; }

	[JsonPropertyName("downloads")]
	/// The number of times this version has been downloaded
	public int Downloads { get; set; }

	[JsonPropertyName("files")]
	/// The number of times this version has been downloaded
	public List<ModrinthModFile> Files { get; set; }
}

public class ModrinthDependency
{
	[JsonPropertyName("version_id")]
	/// The ID of the version that this version depends on
	public string VersionId { get; set; }

	[JsonPropertyName("project_id")]
	/// The ID of the project that this version depends on
	public string ProjectId { get; set; }

	[JsonPropertyName("file_name")]
	/// The file name of the dependency, mostly used for showing external dependencies on modpacks
	public string FileName { get; set; }

	[JsonPropertyName("dependency_type")]
	/// The type of dependency that this version has
	/// One of required, optional, incompatible, embedded
	public string DependencyType { get; set; }
}

public class ModrinthModFile
{
	[JsonPropertyName("hashes")]
	/// A map of hashes of the file. The key is the hashing algorithm and the value is the string version of the hash.
	public ModrinthHashes Hashes { get; set; }

	[JsonPropertyName("url")]
	/// A direct link to the file
	public string Url { get; set; }

	[JsonPropertyName("filename")]
	/// The name of the file
	public string Filename { get; set; }

	[JsonPropertyName("primary")]
	public bool Primary { get; set; }

	[JsonPropertyName("size")]
	/// The size of the file in bytes
	public int Size { get; set; }
}

public class ModrinthHashes
{
	[JsonPropertyName("sha512")]
	public string Sha512 { get; set; }
	[JsonPropertyName("sha1")]
	public string Sha1 { get; set; }
}