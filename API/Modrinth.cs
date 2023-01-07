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
		(string query, (string,string)[] facets, string index = "relevance", int offset = 0, int limit = 10, string filters = null)
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
}

public class ModrinthSearchResponse
{
	[JsonPropertyName("hits")]
	/// Array of objects
	/// The list of results
	public List<ModrinthSearchHit> Hits {get;set;}

	[JsonPropertyName("offset")]
	/// The number of results that were skipped by the query
	public int Offset {get;set;}

	[JsonPropertyName("limit")]
	/// The number of results that were returned by the query
	public int Limit {get;set;}

	[JsonPropertyName("total_hits")]
	/// The total number of results that match the query
	public int TotalHits {get;set;}
}

public class ModrinthSearchHit
{
	[JsonPropertyName("slug")]
	/// The slug of a project, used for vanity URLs. Regex: ^[\w!@$()`.+,"\-']{3,64}$
	public string Slug {get;set;}

	[JsonPropertyName("title")]
	/// The title or name of the project
	public string Title {get;set;}

	[JsonPropertyName("description")]
	/// A short description of the project
	public string Description {get;set;}

	[JsonPropertyName("categories")]
	/// A list of the categories that the project has
	public List<string> Categories {get;set;}

	[JsonPropertyName("client_side")]
	/// The client side support of the project
	/// One of required, optional, unsupported
	public string ClientSide {get;set;}

	[JsonPropertyName("server_side")]
	/// The server side support of the project
	/// One of required, optional, unsupported
	public string ServerSide {get;set;}

	[JsonPropertyName("project_type")]
	/// The project type of the project
	/// One of mod, modpack, resourcepack
	public string ProjectType {get;set;}

	[JsonPropertyName("downloads")]
	/// The total number of downloads of the project
	public int Downloads {get;set;}

	[JsonPropertyName("icon_url")]
	/// The URL of the project's icon
	public string IconUrl {get;set;}

	[JsonPropertyName("project_id")]
	/// The ID of the project
	public string ProjectID {get;set;}

	[JsonPropertyName("author")]
	/// The username of the project's author
	public string Author {get;set;}

	[JsonPropertyName("display_categories")]
	/// A list of the categories that the project has which are not secondary
	public List<string> DisplayCategories {get;set;}

	[JsonPropertyName("versions")]
	/// A list of the minecraft versions supported by the project
	public List<string> Versions {get;set;}

	[JsonPropertyName("follows")]
	/// The total number of users following the project
	public int Follows {get;set;}

	[JsonPropertyName("date_created")]
	/// The date the project was added to search
	public DateTime DateCreated {get;set;}

	[JsonPropertyName("date_modified")]
	/// The date the project was last modified
	public DateTime DateModified {get;set;}

	[JsonPropertyName("latest_version")]
	/// The latest version of minecraft that this project supports
	public string LatestVersion {get;set;}

	[JsonPropertyName("license")]
	/// The license of the project
	public string License {get;set;}

	[JsonPropertyName("gallery")]
	/// All gallery images attached to the project
	public List<string> Gallery {get;set;}
}