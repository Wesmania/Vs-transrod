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
	public class Priv {
		public static FieldInfo Field(BlockEntityStaticTranslocator instance, string name) {
			return instance.GetType().GetField(name,
							   System.Reflection.BindingFlags.NonPublic
							   | System.Reflection.BindingFlags.Instance);
		}
		public static MethodInfo Method(BlockEntityStaticTranslocator instance, string name) {
			return instance.GetType().GetMethod(name,
							    System.Reflection.BindingFlags.NonPublic
							    | System.Reflection.BindingFlags.Instance);
		}
	}
	[HarmonyPatch(typeof(BlockEntityStaticTranslocator), nameof(BlockEntityStaticTranslocator.setupGameTickers))]
	public class BlockEntityStaticTranslocatorHook {

		static void ServerTickReplacement(BlockEntityStaticTranslocator self, float dt)
		{
			var f_sapi = Priv.Field(self, "sapi");
			var sapi = (ICoreServerAPI) f_sapi.GetValue(self);
			if (self.findNextChunk)
			{
				self.findNextChunk = false;

				int addrange = self.MaxTeleporterRangeInBlocks - self.MinTeleporterRangeInBlocks;

				int dx = (int)(self.MinTeleporterRangeInBlocks + sapi.World.Rand.NextDouble() * addrange) * (2 * sapi.World.Rand.Next(2) - 1);
				int dz = (int)(self.MinTeleporterRangeInBlocks + sapi.World.Rand.NextDouble() * addrange) * (2 * sapi.World.Rand.Next(2) - 1);

				int chunkX = (self.Pos.X + dx) / GlobalConstants.ChunkSize;
				int chunkZ = (self.Pos.Z + dz) / GlobalConstants.ChunkSize;

				if (!sapi.World.BlockAccessor.IsValidPos(self.Pos.X + dx, 1, self.Pos.Z + dz))
				{
					self.findNextChunk = true;
					return;
				}

				ChunkPeekOptions opts = new ChunkPeekOptions()
				{
					OnGenerated = (chunks) => {
						var f_TestForExitPoint = Priv.Method(self, "TestForExitPoint");
						f_TestForExitPoint.Invoke(self, new object[] { chunks, chunkX, chunkZ });
					},
						    UntilPass = EnumWorldGenPass.TerrainFeatures,
						    ChunkGenParams = (ITreeAttribute) Priv.Method(self, "chunkGenParams").Invoke(self, new object[] {})
				};

				sapi.WorldManager.PeekChunkColumn(chunkX, chunkZ, opts);
			}
			var f_canTeleport = Priv.Field(self, "canTeleport");
			var f_activated = Priv.Field(self, "activated");

			if ((bool) f_canTeleport.GetValue(self) && (bool) f_activated.GetValue(self))
			{
				try
				{
					var f_HandleTeleportingServer = Priv.Method(self, "HandleTeleportingServer");
					f_HandleTeleportingServer.Invoke(self, new object[] {dt});
				}
				catch (Exception e)
				{
					self.Api.Logger.Warning("Exception when ticking Static Translocator at {0}", self.Pos);
					self.Api.Logger.Error(e);
				}
			}
		}

		static bool Prefix(BlockEntityStaticTranslocator __instance) {
			if (__instance.Api.Side == EnumAppSide.Server)
			{
				var sapi = Priv.Field(__instance, "sapi");
				sapi.SetValue(__instance, __instance.Api as ICoreServerAPI);

				Action<float> doServer = delegate(float dt) {
					ServerTickReplacement(__instance, dt);
				};
				__instance.RegisterGameTickListener(doServer, 250);
			}
			else
			{
				Action<float> doClient = delegate(float dt) {
					var prop = Priv.Method(__instance, "OnClientGameTick");
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
