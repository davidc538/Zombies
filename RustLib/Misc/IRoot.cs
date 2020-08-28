using Oxide.Plugins;
using System;
using UnityEngine;


namespace RustLib
{
	public interface IRoot
	{
		//Config Config();
		//Arena GetArena(string arena);
		PluginTimers Timer();
		void NextTickDo(Action action);
		void Log(string message);
		void Broadcast(string message);
		void SetPlayerDead(ulong player, bool is_dead);
		bool IsPlayerDead(ulong player);
		System.Random Random();
		string AvatarUrl(ulong player);
		void SetBotDirectionVector(ulong user_id, Vector3 direction);
	}
}
