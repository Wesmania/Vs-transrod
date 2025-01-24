using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TransRod {
	[HarmonyPatch(typeof(BlockEntityStaticTranslocator), nameof(BlockEntityStaticTranslocator.setupGameTickers))]
	public class BlockEntityStaticTranslocatorHook {
		static bool Prefix(BlockEntityStaticTranslocator __instance) {
			if (__instance.Api.Side == EnumAppSide.Server)
			{
				var prop = __instance.GetType().GetField("sapi",
									 System.Reflection.BindingFlags.NonPublic
									 | System.Reflection.BindingFlags.Instance);
				prop.SetValue(__instance, __instance.Api as ICoreServerAPI);

				Action<float> doServer = delegate(float dt) {
					var prop = __instance.GetType().GetMethod("OnServerGameTick",
										 System.Reflection.BindingFlags.NonPublic
										 | System.Reflection.BindingFlags.Instance);
					prop.Invoke(__instance, new object[] { dt });
				};
				__instance.RegisterGameTickListener(doServer, 250);
			}
			else
			{
				Action<float> doClient = delegate(float dt) {
					var prop = __instance.GetType().GetMethod("OnClientGameTick",
										 System.Reflection.BindingFlags.NonPublic
										 | System.Reflection.BindingFlags.Instance);
					prop.Invoke(__instance, new object[] { dt });
				};
				__instance.RegisterGameTickListener(doClient, 50);
			}
			return false;
		}
	}

	public class TransRodModSystem : ModSystem {
		private Harmony modhook;

		public TransRodModSystem() {
			modhook = new Harmony("org.4chan.mazornoob.transrod");
		}

		public override void Start(ICoreAPI api)
		{
			api.Logger.Notification("Hello mod");
			modhook.PatchAll();
		}

		public override void StartServerSide(ICoreServerAPI api)
		{
			api.Logger.Notification("Hello server");
		}

		public override void StartClientSide(ICoreClientAPI api)
		{
			api.Logger.Notification("Hello client");
		}
	}
}
