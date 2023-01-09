using System;

public class Profile
{
	public MinecraftProfile MCProfile {get;set;}
	public DateTime MCTokenExpiresIn {get;set;}
	public string MCToken {get;set;}
	
	public string Xuid {get;set;}
	public string MSARefreshToken {get;set;}
}