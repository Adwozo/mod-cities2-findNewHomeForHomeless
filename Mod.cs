using Game.Modding;
using Game;
using HarmonyLib;
using System.Reflection;
using Colossal.Logging;

namespace Homeless_Shelter_Pathfinding
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(Homeless_Shelter_Pathfinding)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        public static Harmony Harmony { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            UnityEngine.Debug.LogWarning("PropertyScoreFix mod: OnLoad called");
            Harmony = new Harmony("com.adwozo.propertyScoreFix");
            Harmony.PatchAll(Assembly.GetExecutingAssembly());
            UnityEngine.Debug.LogWarning("PropertyScoreFix mod: Patches applied");
        }

        public void OnDispose()
        {
            if (Harmony != null)
            {
                Harmony.UnpatchAll(Harmony.Id);
                UnityEngine.Debug.LogWarning("PropertyScoreFix mod unpatched successfully.");
            }
        }
    }
}