using Godot;
using System;
using System.IO;

public static class Utils
{
	public static string BytesToString(this long byteCount)
	{
		string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
		if (byteCount == 0)
			return "0" + suf[0];
		long bytes = Math.Abs(byteCount);
		int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
		double num = Math.Round(bytes / Math.Pow(1024, place), 1);
		return (Math.Sign(byteCount) * num).ToString() + suf[place];
	}

	public static ImageTexture LoadTexture(this Stream s, string ext)
	{
		ext = ext.ToLower();
		var imgms = new MemoryStream();
		s.Seek(0, SeekOrigin.Begin);
		s.CopyTo(imgms);

		var img = new Image();
		switch (ext)
		{
			case "jpg":
			case "jpeg":
				// JPEG decoding is broken
				return null;
				img.LoadJpgFromBuffer(imgms.ToArray());
				break;
			case "png":
				img.LoadPngFromBuffer(imgms.ToArray());
				break;
			default:
			case "svg":
				return null;
		}

		return ImageTexture.CreateFromImage(img);

	}
}