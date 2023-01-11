using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

public static class WebCache
{
	public static async Task<Stream> Get(string url)
	{
		var urlb = Encoding.UTF8.GetBytes(url);
		var h = Convert.ToHexString(MD5.Create().ComputeHash(urlb)).ToLower();

		Directory.CreateDirectory(Paths.WebCache + "/" + h[0..2]);
		var cacheFilePath = Paths.WebCache + "/" + h[0..2] + "/" + h;

		if (!File.Exists(cacheFilePath))
		{
			var s = await new RequestBuilder(url).Get<Stream>();
			var f = File.OpenWrite(cacheFilePath);
			await s.CopyToAsync(f);
			f.Close();

			s.Seek(0,SeekOrigin.Begin);
			return s;
		}
		else return File.OpenRead(cacheFilePath);
	}
}