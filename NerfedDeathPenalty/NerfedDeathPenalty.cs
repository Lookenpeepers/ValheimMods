using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace NerfedDeathPenalty
{
    [BepInPlugin("Lookenpeepers-NerfedDeathPenalty", "Nerfed Death Penalty", "1.0.0")]
    [HarmonyPatch]
    public class NerfedDeathPenalty : BaseUnityPlugin
    {
        static Player _player;
        public static ConfigEntry<float> expLoss;
        void Awake()
        {
            expLoss = Config.Bind("1 - Settings", "Exp Loss", 0.10f, "The percentage of exp lost on death (between 0.0 and 1.0)");
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        static List<float> accumulators = new List<float>();
        static string output = "\n";
        static Skills playerSkillsComponent;
        static List<Skills.Skill> playerSkills;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.OnDestroy))]
        private static void PlayerAwawke_Patch(Player __instance)
        {
            playerSkillsComponent = __instance.GetSkills();
            playerSkills = playerSkillsComponent.GetSkillList();
            playerSkillsComponent.GetSkillFactor(Skills.SkillType.Axes);
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Skills), "Awake")]
        private static void SkillsAwake_Patch(Skills __instance)
        {
            //playerSkills = __instance.GetSkillList();
            //output += "Skill count : " + __instance.GetSkillList().Count + "\n";
            __instance.m_DeathLowerFactor = 0;
            //foreach (Skills.Skill sd in playerSkills)
            //{
            //    //sd.Raise(-1);
            //    output += "Skill : " + sd.m_info.m_skill.ToString() + " exp = " + sd.m_info.m_increseStep +
            //             " Penalty = " + sd.m_accumulator * expLoss.Value + "\n";
            //    sd.Raise(1);
            //    output += sd.GetLevelPercentage();
            //}
            //Debug.Log(output);
        }
    }
}
