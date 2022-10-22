using Godot;
using System;
using Godot.Collections;
using System.Threading.Tasks;

public static class Request
{
	public static async Task<(int code, byte[] result)> Get(this Node n, string url, string[] headers, string body = "")
			=> await MakeRequest(n, HTTPClient.Method.Get, url, headers, body);

	public static async Task<(int code, byte[] result)> Post(this Node n,  string url, string[] headers, string body = "")
		=> await MakeRequest(n, HTTPClient.Method.Post, url, headers, body);

	public static async Task<(int code, byte[] result)> MakeRequest(this Node n, HTTPClient.Method method, string url, string[] headers, string body = "")
	{
		var http = new HTTPClient();

		var uri = new Uri(url);
		http.ConnectToHost(uri.Scheme + "://" + uri.Host);

		while (http.GetStatus() == HTTPClient.Status.Connecting || http.GetStatus() == HTTPClient.Status.Resolving)
		{
			http.Poll();
			await n.ToSignal(n.GetTree(), "process_frame");
		}

		var err = http.Request(method, url, headers, body);

		while (http.GetStatus() == HTTPClient.Status.Requesting)
		{
			err = http.Poll();
			await n.ToSignal(n.GetTree(), "process_frame");
		}

		System.Collections.Generic.List<byte> resbody = new();
		while (http.GetStatus() == HTTPClient.Status.Body)
		{
			byte[] chunk = http.ReadResponseBodyChunk();
			if (chunk.Length == 0)
				await n.ToSignal(n.GetTree(), "process_frame");
			else
				resbody.AddRange(chunk);
		}

		return (http.GetResponseCode(), resbody.ToArray());
	}
}