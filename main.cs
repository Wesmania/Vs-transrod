using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace TransRod {
	public class TransRodModSystem : ModSystem {
		public override void Start(ICoreAPI api)
		{
			api.Logger.Notification("Hello mod");
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
