using HarmonyLib;
using System;
using System.Collections.Generic;
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
	public class TransRod: BlockEntity {

		BlockEntityAnimationUtil animUtil {
			get { return GetBehavior<BEBehaviorAnimatable>().animUtil; }
		}

		public override void Initialize(ICoreAPI api) {
			base.Initialize(api);
			if (api.Side == EnumAppSide.Client) {
				animUtil?.InitializeAnimator("transrod", null, null, new Vec3f(0, getRotation(), 0));
				animUtil?.StartAnimation(new AnimationMetaData() { Animation = "gearrotation", Code = "gearrotation" });
			}
		}

		public int getRotation()
		{
			int rot = 0;
			switch (Block.LastCodePart())
			{
				case "north": rot = 0; break;
				case "east": rot = 270; break;
				case "south": rot = 180; break;
				case "west": rot = 90; break;
			}
			return rot;
		}
	}

	public class BlockTransRod: Block {

		const int MAX_ATTEMPTS = 100;
		public int TeleportAttempts = 0;
		public bool CanDropItem = true;

		public Shape GetShape() {
			return Vintagestory.API.Common.Shape.TryGet(api, Shape.Base);
		}

		public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1) {
			List<ItemStack> stuff = new List<ItemStack>();
			if (CanDropItem) {
				stuff.Add(new ItemStack(world.GetBlock(new AssetLocation("transrod:transrod-north")), 1));
			}
			GetAdjacentRod(world, pos);
			return stuff.ToArray();
		}

		public bool CanCoax() {
			if (TeleportAttempts < MAX_ATTEMPTS) {
				TeleportAttempts += 1;
				CanDropItem = false;
				return true;
			} else {
				return false;
			}
		}

		public static (BlockTransRod, BlockFacing)? GetAdjacentRod(IWorldAccessor w, BlockPos tl) {
			IBlockAccessor ba = w.BlockAccessor;
			foreach (var face in new [] {BlockFacing.NORTH, BlockFacing.EAST, BlockFacing.SOUTH, BlockFacing.WEST}) {
				Block adjacent = PosUtil.GetBlockOnSide(ba, tl, face);
				// FIXME: no way to check for transrod: domain without messing around with strings.
				// Sucks to be you if your mod has a "transrod" entity!
				if (adjacent.CodeWithoutParts(1) == "transrod") {
					return ((BlockTransRod) adjacent, face);
				}
			}
			return null;
		}

		public static (int, int)? GetRandPosFromCone(IWorldAccessor w, int mindist, int maxdist, BlockFacing face) {
			// Picks a randomlocation in a trapezoid made from a 90
			// degree cone in a cardinal direction truncated by
			// minimum and maximum distance.
			//
			// Something like this:
			//
			//   -------------------
			//    \               /
			//     \             /
			//      \           /
			//       -----------
			//
			//
			//
			//
			//            *

			// Cardinal directions to coordinates:
			// North is towards negative Z
			// South is towards positive Z
			// West is towards negative X
			// East is towards positive X

			// First, randomize any point between mindist and maxdist away.
			int addrange = maxdist - mindist;
			int dx = (int)(mindist + w.Rand.NextDouble() * addrange) * (2 * w.Rand.Next(2) - 1);
			int dz = (int)(mindist + w.Rand.NextDouble() * addrange) * (2 * w.Rand.Next(2) - 1);

			// Now, if the point did not land in the northern cone, rotate it by 90 degrees until it does.
			while (dz > 0 || Math.Abs(dx) > Math.Abs(dz)) {
				(dx, dz) = (dz, -dx);
			}

			// Finally, rotate the point to the desired cone.
			if (face == BlockFacing.NORTH) {
				// Already pointing north
			} else if (face == BlockFacing.SOUTH) {
				// Just reverse both coordinates.
				(dx, dz) = (-dx, -dz);
			} else if (face == BlockFacing.WEST) {
				// Transpose coordinates. Now dx <= 0 and abs(dz) <= abs(dx), so it's western cone.
				(dx, dz) = (dz, dx);
			} else if (face == BlockFacing.EAST) {
				// Same, but also reverse coordinates.
				(dx, dz) = (-dz, -dx);
			} else {
				w.Api.Logger.Warning("Unexpected block face {0}", face);
				return null;
			}
			return (dx, dz);
		}

		public static (int, int)? TryCoaxCoordinates(IWorldAccessor w, BlockPos tl, int mindist, int maxdist) {
			var adjacent_trans_rod = BlockTransRod.GetAdjacentRod(w, tl);
			if (adjacent_trans_rod == null) {
				return null;
			}

			var (rod, face) = adjacent_trans_rod.Value;
			w.Api.Logger.Notification("Translocator coaxing rod found in direction {0}", face);
			if (!rod.CanCoax()) {
				return null;
			}
			return BlockTransRod.GetRandPosFromCone(w, mindist, maxdist, face);
		}
	}

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
				int dx, dz;

				var coaxedCoords = BlockTransRod.TryCoaxCoordinates(sapi.World, self.Pos,
										    self.MinTeleporterRangeInBlocks,
										    self.MaxTeleporterRangeInBlocks);
				if (coaxedCoords != null) {
					(dx, dz) = coaxedCoords.Value;
					sapi.Logger.Notification("Trying coaxed coordinates ({0}, {1})", dx, dz);
				} else {
					// Do the original logic.
					int addrange = self.MaxTeleporterRangeInBlocks - self.MinTeleporterRangeInBlocks;
					dx = (int)(self.MinTeleporterRangeInBlocks + sapi.World.Rand.NextDouble() * addrange) * (2 * sapi.World.Rand.Next(2) - 1);
					dz = (int)(self.MinTeleporterRangeInBlocks + sapi.World.Rand.NextDouble() * addrange) * (2 * sapi.World.Rand.Next(2) - 1);
					sapi.Logger.Notification("Trying original coordinates ({0}, {1})", dx, dz);
				}

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
			api.RegisterBlockClass("transrod:BlockTransRod", typeof(BlockTransRod));
			api.RegisterBlockEntityClass("transrod:TransRod", typeof(TransRod));
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
