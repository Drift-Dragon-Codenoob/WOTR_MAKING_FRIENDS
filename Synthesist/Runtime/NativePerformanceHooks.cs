#if SYNTHESIST_ACCEPTANCE
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kingmaker;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    // Temporary development diagnostics around native work; never in production.
    [HarmonyPatch]
    internal static class NativePerformanceHooks
    {
        private static readonly Dictionary<MethodBase, string> names = new();
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in typeof(Game).Assembly.GetTypes())
            {
                if (type.ContainsGenericParameters) continue;
                bool controller = type.Namespace != null && (type.Namespace.StartsWith("Kingmaker.Controllers", StringComparison.Ordinal) ||
                    type.Namespace.StartsWith("Kingmaker.AI", StringComparison.Ordinal));
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (method.ContainsGenericParameters || method.GetMethodBody() == null) continue;
                    bool tick = (controller || type == typeof(Game)) && method.Name == "Tick" && method.ReturnType == typeof(void) && method.GetParameters().Length == 0;
                    bool actionBar = type.FullName.StartsWith("Kingmaker.UI.MVVM._VM.ActionBar.", StringComparison.Ordinal) ||
                        type.FullName.StartsWith("Kingmaker.UI.UnitSettings.MechanicActionBarSlot", StringComparison.Ordinal);
                    bool ability = type.FullName == "Kingmaker.UnitLogic.Abilities.AbilityData" &&
                        (method.Name.Contains("Available") || method.Name.Contains("CanBeCast") || method.Name == "CanTarget");
                    if (!tick && !actionBar && !ability) continue;
                    names[method] = type.Name + "." + method.Name;
                    yield return method;
                }
            }
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            foreach (string typeName in new[] { "UnityEngine.UI.CanvasUpdateRegistry", "UnityEngine.UI.LayoutRebuilder", "TMPro.TextMeshProUGUI" })
            {
                var type = assembly.GetType(typeName);
                if (type == null) continue;
                foreach (string methodName in new[] { "PerformUpdate", "Rebuild", "GenerateTextMesh", "OnPreRenderCanvas" })
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (method.Name != methodName || method.GetMethodBody() == null) continue;
                    names[method] = type.Name + "." + method.Name;
                    yield return method;
                }
            }
        }
        [HarmonyPriority(Priority.First)]
        private static void Prefix(MethodBase __originalMethod, out RuntimePerformance.Scope __state)
            => __state = RuntimePerformance.Active ? RuntimePerformance.Measure(names[__originalMethod]) : default;
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(RuntimePerformance.Scope __state) => __state.Dispose();
    }

}
#endif
