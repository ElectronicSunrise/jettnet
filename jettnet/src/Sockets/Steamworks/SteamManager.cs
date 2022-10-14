using jettnet.core;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jettnet.steamworks.core
{
    /// <summary>
    /// Manages steamworks initialization and callbacks
    /// </summary>
    public class SteamManager
    {

        private SteamAPIWarningMessageHook_t warningHook;
        private jettnet.core.Logger logger;

        public bool Initialized { get; private set; }

        public SteamManager(jettnet.core.Logger logger, Action OnQuitRequest, uint appID=480)
        {
            this.logger = logger;

            if (!Packsize.Test())
            {
                logger.Log("[Steamworks.NET] Packsize Test returned false, the wrong version of Steamworks.NET is being run in this platform.", jettnet.core.LogLevel.Error);
            }

            if (!DllCheck.Test())
            {
                logger.Log("[Steamworks.NET] DllCheck Test returned false, One or more of the Steamworks binaries seems to be the wrong version.", jettnet.core.LogLevel.Error);
            }

            try
            {
                if (SteamAPI.RestartAppIfNecessary((AppId_t)appID))
                {
                    OnQuitRequest();
                    return;
                }
            }
            catch (DllNotFoundException exception)
            {
                logger.Log("[Steamworks.NET] Could not load [lib]steam_api.dll/so/dylib. It's likely not in the correct location. Refer to the README for more details.", jettnet.core.LogLevel.Error);
                logger.Log(exception, jettnet.core.LogLevel.Error);

                OnQuitRequest();
                return;
            }

            Initialized = SteamAPI.Init();

            if (!Initialized)
            {
                //Error occured
                logger.Log("[Steamworks.NET] SteamAPI_Init() failed. Refer to Valve's documentation or the comment above this line for more information.", jettnet.core.LogLevel.Error);
            }

            //Setup warning hook
            warningHook = new SteamAPIWarningMessageHook_t(OnWarning);
            SteamClient.SetWarningMessageHook(warningHook);
        }

        private void OnWarning(int nSeverity, StringBuilder pchDebugText)
        {
            logger.Log($"Message severity: {nSeverity}, Warning: {pchDebugText}");
        }

        public void Tick()
        {
            if (!Initialized)
            {
                logger.Log("Cannot tick when uninitialized.", LogLevel.Error);
                return;
            }

            SteamAPI.RunCallbacks();
        }

        public void Shutdown()
        {
            SteamAPI.Shutdown();
        }
    }
}
