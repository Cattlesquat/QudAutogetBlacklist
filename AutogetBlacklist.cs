using System;
using System.Collections.Generic;
using System.Linq;
using XRL;
using XRL.UI;
using XRL.Core;
using XRL.World;
using XRL.World.Anatomy;
using XRL.World.Parts;
using XRL.World.Parts.Skill;
using XRL.World.Tinkering;
using UnityEngine;
using HarmonyLib;

namespace XRL.World.Parts
{
	public class Cattlesquat_AutogetBlacklist : IPlayerSystem
	{
		public override void RegisterPlayer(GameObject Player, IEventRegistrar Registrar)
		{
			Registrar.Register(OwnerGetInventoryActionsEvent.ID);   // Add the blacklist commands to game objects' twiddle menus
			Registrar.Register(InventoryActionEvent.ID);            // Process blacklist toggles
		}
		
		public override bool HandleEvent(OwnerGetInventoryActionsEvent E)
		{
			if (E.Object.HasPart<Examiner>() && E.Object.Understood() && !E.Object.IsSpecialItem() && ((E.Actor == null) || !E.Actor.IsConfused) && Options.AutogetArtifacts && (E.Object.GetPart<Examiner>().Complexity > 0))
			{
				if (!The.Player.HasSkill("Tinkering_Disassemble") || (!Tinkering_Disassemble.CanBeConsideredScrap(E.Object) && !TinkeringHelpers.ConsiderStandardScrap(E.Object)))
				{
					if (Cattlesquat_AutogetBlacklist_Examiner_Patcher.CheckBlacklistToggle(E.Object))
					{
						E.AddAction(
							Name: "Toggle Blacklist",
							Display: "start collecting these with Autoget",
							Command: "ToggleBlacklist",
							Key: 'S',
							PreferToHighlight: "start",
							WorksAtDistance: true,
							FireOnActor: true
						);
					}
					else
					{
						E.AddAction(
							Name: "Toggle Blacklist",
							Display: "stop collecting these with Autoget",
							Command: "ToggleBlacklist",
							Key: 'S',
							PreferToHighlight: "stop",
							WorksAtDistance: true,
							FireOnActor: true
						);
					}
				}
			}

            if (((E.Object.GetInventoryCategory() == "Food") && Options.AutogetFood) || (E.Object.Blueprint == "Witchwood Bark")) {
				if (!Options.AutogetSpecialItems || !E.Object.IsSpecialItem()) {
					if (Cattlesquat_AutogetBlacklist_Examiner_Patcher.CheckBlacklistToggle(E.Object))
					{
						E.AddAction(
							Name: "Toggle Blacklist",
							Display: "start collecting these with Autoget",
							Command: "ToggleBlacklist",
							Key: 'S',
							PreferToHighlight: "start",
							WorksAtDistance: true,
							FireOnActor: true
						);
					}
					else
					{
						E.AddAction(
							Name: "Toggle Blacklist",
							Display: "stop collecting these with Autoget",
							Command: "ToggleBlacklist",
							Key: 'S',
							PreferToHighlight: "stop",
							WorksAtDistance: true,
							FireOnActor: true
						);
					}
                }
            }

			return base.HandleEvent(E);            
		}
		
		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "ToggleBlacklist")
			{
				if (E.Actor.IsPlayer())
				{
					//XRL.Messages.MessageQueue.AddPlayerMessage("TOGGLING Blacklist: " + E.Item.Blueprint);
					Cattlesquat_AutogetBlacklist_Examiner_Patcher.ToggleBlacklist(E.Item);

                    if (Cattlesquat_AutogetBlacklist_Examiner_Patcher.CheckBlacklistToggle(E.Item)) {
						Popup.Show("You resolve to stop collecting *any* kind of " + E.Item.BaseDisplayName + ".");
					}
					else {
						Popup.Show("You resume collecting all kinds of " + E.Item.BaseDisplayName + ".");
                    }
				}
			}
			return base.HandleEvent(E);
		}
	}

    // Blacklisting for Food pickups (die, Qudzu Stem!!!)
	[HarmonyPatch(typeof(XRL.World.GameObject))]
    public class Cattlesquat_AutogetBlacklist_GameObject_Patcher 
    {
        [HarmonyPatch(nameof(XRL.World.GameObject.ShouldAutoget), new Type[] {} )]
    	static bool Prefix(GameObject __instance, ref bool __result) {
            if (!Options.AutogetSpecialItems || !__instance.IsSpecialItem())
            {
                if (__instance.GetInventoryCategory() == "Food") {
                    if (Cattlesquat_AutogetBlacklist_Examiner_Patcher.CheckBlacklistToggle(__instance)) {
                        __result = false;
                        return false;
                    }
                }
            }

            return true;
        }
    }

    // If we're not picking up a food item, don't harvest it either
    [HarmonyPatch(typeof(XRL.World.Parts.Harvestable))]
    public class Cattlesquat_AutogetBlacklist_Harvestable_Patcher
    {
		[HarmonyPatch(nameof(XRL.World.Parts.Harvestable.AttemptHarvest), new Type[] { typeof(GameObject), typeof(bool), typeof(string), typeof(Cell), typeof(List<GameObject>) } )]
        static bool Prefix (Harvestable __instance, ref bool __result, GameObject who, bool Automatic, string Verb, Cell FromCell, List<GameObject> Tracking) {
            if (Automatic && who.IsPlayer()) {
                string toggleKey = "Cattlesquat_Blacklist_" + __instance.OnSuccess; // The OnSuccess is a Blueprint, so we check for that specific blueprint being blacklisted
                if (The.Game.GetBooleanGameState(toggleKey)) {
                    __result = false;
                    return false;
                }
            } 
            return true;
        }
    } 
	
	
	[HarmonyPatch(typeof(XRL.World.Parts.Examiner))]
	public class Cattlesquat_AutogetBlacklist_Examiner_Patcher
	{
		[HarmonyPatch(nameof(XRL.World.Parts.Examiner.HandleEvent), new Type[] { typeof(AutoexploreObjectEvent) } )]
		static bool Prefix(Examiner __instance, ref bool __result, AutoexploreObjectEvent E)
		{
			if (__instance.Complexity > 0 && E.Command == null && Options.AutogetArtifacts && __instance.ParentObject.CanAutoget())
			{
				if (!E.Actor.IsPlayer()) return true;
				if (!__instance.ParentObject.Understood()) return true; // If we don't understand the object yet, it can't be blacklisted
                if (__instance.ParentObject.IsSpecialItem()) return true; // Relics and quest items and the like
				if (The.Player.HasSkill("Tinkering_Disassemble"))
				{
					if (Tinkering_Disassemble.CanBeConsideredScrap(__instance.ParentObject) || TinkeringHelpers.ConsiderStandardScrap(__instance.ParentObject)) return true; // If player could just "consider it scrap" then we don't blacklist
				}

				if (!CheckBlacklistToggle(__instance.ParentObject)) return true; // Check to see if this item has been blacklisted - if it hasn't, we allow the regular method to run, which will autoget it

				// If we made it here, the item has been actively blacklisted, so we just call the base HandleEvent method and otherwise get out, cancelling the original might-autoget routine.  				
	 			//__result = ((__instance as IComponent<GameObject>).HandleEvent(E)); // Call base method
                __result = true;
				return false;
			}

			return true;
		}
		
		public static string ToggleKey(GameObject obj)
		{
			return "Cattlesquat_Blacklist_" + obj.Blueprint; //No mod profile - don't want to pick up every variation either
			//return "Cattlesquat_Blacklist_" + Tinkering_Disassemble.ToggleKey(obj);
		}
		
		public static bool CheckBlacklistToggle(GameObject obj)
		{
			return The.Game.GetBooleanGameState(ToggleKey(obj)) && obj.Understood();
		}
        
		public static void SetBlacklistToggle(GameObject obj, bool flag)
		{
			if (flag)
			{
				The.Game.SetBooleanGameState(ToggleKey(obj), true);
			}
			else
			{
				The.Game.RemoveBooleanGameState(ToggleKey(obj));
			}
		}

		public static void ToggleBlacklist(GameObject obj)
		{
			SetBlacklistToggle(obj, !CheckBlacklistToggle(obj));
		}
	}

    
    // If "broader treat-as-scrap" option selected, then override Treat-as-Scrap toggles to be broader
    [HarmonyPatch(typeof(XRL.World.Parts.Skill.Tinkering_Disassemble))]
    public class Cattlesquat_AutogetBlacklist_TinkeringDisassemble_Patcher
    {

		public static string ToggleKey(GameObject obj)
		{
			return "Cattlesquat_ScrapBlacklist_" + obj.Blueprint; 
		}
		
		public static bool CheckScrapBlacklistToggle(GameObject obj)
		{
			return The.Game.GetBooleanGameState(ToggleKey(obj)) && obj.Understood();
		}
        
		public static void SetScrapBlacklistToggle(GameObject obj, bool flag)
		{
			if (flag)
			{
				The.Game.SetBooleanGameState(ToggleKey(obj), true);
			}
			else
			{
				The.Game.RemoveBooleanGameState(ToggleKey(obj));
			}
		}

		public static void ToggleScrapBlacklist(GameObject obj)
		{
			SetScrapBlacklistToggle(obj, !CheckScrapBlacklistToggle(obj));
		}

        

    	[HarmonyPatch(nameof(XRL.World.Parts.Skill.Tinkering_Disassemble.CheckScrapToggle), new Type[] { typeof(GameObject) } )]
    	static bool Prefix(ref bool __result, GameObject obj)
    	{
            if (Options.GetOptionBool("OptionBroaderTreatAsScrap")) {
            	__result = CheckScrapBlacklistToggle(obj);
                return false;
            }
            return true;
        }

    	[HarmonyPatch(nameof(XRL.World.Parts.Skill.Tinkering_Disassemble.SetScrapToggle), new Type[] { typeof(GameObject), typeof(bool) } )]
    	static bool Prefix(GameObject obj, bool flag)
    	{
            if (Options.GetOptionBool("OptionBroaderTreatAsScrap")) {
				SetScrapBlacklistToggle(obj, flag);
                if (flag) {
                    Popup.Show("You start treating all kinds of " + obj.BaseDisplayName + " as scrap.");
                }
                else
                {
                    Popup.Show("You stop treating any kinds of " + obj.BaseDisplayName + " as scrap.");
				}
                return false;
            }
            return true;
        }                        
    }

    /*
    public static bool CheckScrapToggle(GameObject obj)
    {
        return The.Game.GetBooleanGameState(ToggleKey(obj)) && obj.Understood();
    }
    
    public static void SetScrapToggle(GameObject obj, bool flag)
    {
        if (flag)
        {
            The.Game.SetBooleanGameState(ToggleKey(obj), true);
        }
        else
        {
            The.Game.RemoveBooleanGameState(ToggleKey(obj));
        }
    }
    */

	
	
	
	[PlayerMutator]
	public class Cattlesquat_AutogetBlacklist_PlayerMutator : IPlayerMutator
	{
		// Adds our system for new games
		public void mutate(GameObject player)
		{ 
			//player.RequirePart<Cattlesquat_AutogetBlacklist_BootstrapPart>();
			XRL.The.Game.RequireSystem<Cattlesquat_AutogetBlacklist>();
		}
	}
	
	[HasCallAfterGameLoadedAttribute]
	public class Cattlesquat_AutogetBlacklist_LoadGameHandler
	{
		[CallAfterGameLoadedAttribute]
		public static void LoadGameCallback()
		{
			// Called whenever loading a save game -- retrofit our systems into play
			//var player = XRLCore.Core?.Game?.Player?.Body;
			//player?.RequirePart<Cattlesquat_AutogetBlacklist_BootstrapPart>();
			The.Game.RequireSystem<Cattlesquat_AutogetBlacklist>();
		}
	}
	
	/*
	public class Cattlesquat_AutogetBlacklist_BootstrapPart : IPlayerPart
	{
		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("EncumbranceChanged");
			base.Register(Object, Registrar);
		}
    
		public override bool FireEvent(Event E)
		{
			if (E.ID == "EncumbranceChanged")
			{
				XRL.The.Game.RequireSystem<Cattlesquat_AutogetBlacklist>();
			}
			return base.FireEvent(E);
		}
	}
	*/
}
