using System.Threading.Tasks;

public static class Adoptium
{
	public static async Task<string> GetJRE(string os, string arch, int major)
	{
		return await new RequestBuilder($"https://api.adoptium.net/v3/assets/latest/{major}/hotspot?os={os}&architecture={arch}&image_type=jre")
			.Get<string>();
	}
}