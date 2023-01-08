using System;
using System.IO;
using System.Web;
using System.Text;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;

public class RequestBuilder
{
	public static bool DebugPrint = false;

	public string Url;
	public Dictionary<string, string> QuerParameters = new();
	public Dictionary<string, string> Headers = new();

	public RequestBuilder(string url)
	{
		this.Url = url;
	}

	public RequestBuilder Query(string q, string v)
	{
		if (v != null)
			QuerParameters[q] = v;

		return this;
	}

	public RequestBuilder Query(string q, int v)
	{
		return Query(q, v.ToString());
	}

	public RequestBuilder Header(string h, string v)
	{
		Headers[h] = v;
		return this;
	}

	public async Task<T> Get<T>()
		=> await Send<T>(HttpMethod.Get);

	public async Task<T> Post<T>()
		=> await Send<T>(HttpMethod.Post);

	public async Task<T> PostJson<T>(string json)
		=> await Send<T>(HttpMethod.Post, Encoding.UTF8.GetBytes(
				json), "application/json");

	public async Task<T> Post<T>(Dictionary<string, string> data)
	{
		return await Send<T>(HttpMethod.Post,
			Encoding.UTF8.GetBytes(
				String.Join('&',data.Select(d => HttpUtility.UrlEncode(d.Key) + "=" + HttpUtility.UrlEncode(d.Value)))),
			"application/x-www-form-urlencoded");
	}

	public async Task<T> Send<T>(HttpMethod method, byte[] data = null, string mime = null)
	{
		var qurl = Url;

		if (QuerParameters.Any())
		{
			qurl += "?" + string.Join('&',
				QuerParameters.Select(q =>
				$"{HttpUtility.UrlEncode(q.Key)}={HttpUtility.UrlEncode(q.Value)}"));
		}

		var m = new HttpRequestMessage(method, qurl);

		if (data != null)
		{
			var bac = new ByteArrayContent(data, 0, data.Length);
			bac.Headers.Add("Content-Type", mime);
			m.Content = bac;
		}

		m.Headers.UserAgent.Add(new ProductInfoHeaderValue("MCelium", "0.0.1"));
		foreach (var h in Headers)
			m.Headers.Add(h.Key, h.Value);

		var http = new HttpClient();
		var r = await http.SendAsync(m);

		if(DebugPrint)
		{
			Godot.GD.Print(r.StatusCode);
			Godot.GD.Print(await r.Content.ReadAsStringAsync());
			Godot.DisplayServer.ClipboardSet(await r.Content.ReadAsStringAsync());
		}

		if (typeof(T) == typeof(string))
		{
			return (T)(object)(await r.Content.ReadAsStringAsync());
		}
		else if (typeof(T) == typeof(Stream))
		{
			return (T)(object)(await r.Content.ReadAsStreamAsync());
		}
		else
		{
			return (T)(JsonSerializer.Deserialize(await r.Content.ReadAsStreamAsync(), typeof(T)));
		}
	}
}