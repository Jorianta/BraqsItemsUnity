﻿using BraqsItems.Misc;
using R2API;
using RoR2;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using static BraqsItems.Misc.CharacterEvents;
using static BraqsItems.Util.Helpers;

namespace BraqsItems
{
    internal class ExplosionFrenzy
    {
        public static ItemDef itemDef;

        public static bool isEnabled = true;
        public static float baseBurn = 0.5f;
        public static float burnPerStack = 0.5f;
        public static float explosionBoost = 0.1f;
        public static int baseMaxBonus = 10;
        public static int maxBonusPerStack = 10;


        internal static void Init()
        {
            if (!isEnabled) return;

            Log.Info("Initializing My Manifesto Item");
            //ITEM//
            itemDef = GetItemDef("ExplosionFrenzy");

            ItemDisplayRuleDict displayRules = new ItemDisplayRuleDict(null);
            ItemAPI.Add(new CustomItem(itemDef, displayRules));


            Hooks();

            Log.Info("My Manifesto Initialized");
        }

        public static void Hooks()
        {
            On.RoR2.CharacterBody.OnInventoryChanged += CharacterBody_OnInventoryChanged;
            On.RoR2.BlastAttack.Fire += BlastAttack_Fire;

            DotController.onDotInflictedServerGlobal += DotController_onDotInflictedServerGlobal;
            On.RoR2.DotController.OnDotStackRemovedServer += DotController_OnDotStackRemovedServer;


            Stats.StatsCompEvent.StatsCompRecalc += StatsCompEvent_StatsCompRecalc;
        }

        private static void DotController_onDotInflictedServerGlobal(DotController dotController, ref InflictDotInfo inflictDotInfo)
        {
            if (inflictDotInfo.dotIndex == DotController.DotIndex.Burn || inflictDotInfo.dotIndex == DotController.DotIndex.StrongerBurn)
            {
                if (dotController.victimBody && dotController.victimHealthComponent && dotController.victimHealthComponent.alive && dotController.victimBody.master && dotController.victimBody.master.TryGetComponent(out BraqsItems_CharacterEventComponent eventComponent) &&
                    inflictDotInfo.attackerObject.TryGetComponent(out CharacterBody body) && body.TryGetComponent(out BraqsItems_ExplosionFrenzyBehavior component))
                {
                    component.AddVictimBurnStack(dotController.victimBody);
                    eventComponent.OnCharacterDeath += component.RemoveAllVictimBurnStacks;
                }
            }
        }

        private static void DotController_OnDotStackRemovedServer(On.RoR2.DotController.orig_OnDotStackRemovedServer orig, DotController self, object dotStack)
        {
            DotController.DotStack stack = (DotController.DotStack)dotStack;

            if (stack.attackerObject && stack.attackerObject.TryGetComponent(out CharacterBody body) && body.TryGetComponent(out BraqsItems_ExplosionFrenzyBehavior component) && self.victimHealthComponent
                && (stack.dotIndex == DotController.DotIndex.Burn || stack.dotIndex == DotController.DotIndex.StrongerBurn))
            {
                component.RemoveVictimBurnStack(self.victimBody);
            }

            orig(self, dotStack);
        }

        private static void CharacterBody_OnInventoryChanged(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            self.AddItemBehavior<BraqsItems_ExplosionFrenzyBehavior>(self.inventory.GetItemCount(itemDef));
            orig(self);
        }

        private static BlastAttack.Result BlastAttack_Fire(On.RoR2.BlastAttack.orig_Fire orig, BlastAttack self)
        {

            //Apply burn
            BlastAttack.Result result = orig(self);

            if (self.attacker && self.attacker.TryGetComponent(out CharacterBody body) && body.inventory)
            {
                int stacks = body.inventory.GetItemCount(itemDef);
                if (stacks > 0 && result.hitCount > 0)
                {
                    float damage = ((stacks - 1) * burnPerStack + baseBurn) * self.baseDamage;

                    for (int i = 0; i < result.hitCount; i++)
                    {
                        HurtBox hurtBox = result.hitPoints[i].hurtBox;

                        if ((bool)hurtBox.healthComponent)
                        {
                            InflictDotInfo inflictDotInfo = default(InflictDotInfo);
                            inflictDotInfo.victimObject = hurtBox.healthComponent.gameObject;
                            inflictDotInfo.attackerObject = self.attacker;
                            inflictDotInfo.totalDamage = damage;
                            inflictDotInfo.dotIndex = DotController.DotIndex.Burn;
                            inflictDotInfo.damageMultiplier = 1f;
                            InflictDotInfo dotInfo = inflictDotInfo;

                            StrengthenBurnUtils.CheckDotForUpgrade(body.inventory, ref dotInfo);
                            
                            DotController.InflictDot(ref dotInfo);
                        }
                    }
                }
            }
            
            return result;
        }

        public static void StatsCompEvent_StatsCompRecalc(object sender, Stats.StatsCompRecalcArgs args)
        {
            if (args.Stats && NetworkServer.active)
            {
                if (args.Stats.body && args.Stats.body.TryGetComponent(out BraqsItems_ExplosionFrenzyBehavior component))
                {
                    int bonus = Math.Min(component.victims.Count, component.maxBonus);
                    if (bonus > 0)
                    {
                        args.Stats.blastRadiusBoostAdd *= (bonus) * explosionBoost + 1;
                    }
                }
            }
        }

        public class BraqsItems_ExplosionFrenzyBehavior : CharacterBody.ItemBehavior
        {
            public int maxBonus;
            //TODO: not use dictionary, very slow
            public Dictionary<CharacterBody, int> victims = new Dictionary<CharacterBody, int>();

            private void Start()
            {
                Log.Debug("ExplosionFrenzyBehavior:Start()");
                maxBonus = maxBonus * (stack-1) + baseMaxBonus;
            }

            private void OnDestroy()
            {
                Log.Debug("ExplosionFrenzyBehavior:OnDestroy()");
            }

            public void AddVictimBurnStack(CharacterBody body)
            {
                if (!victims.ContainsKey(body))
                {
                    victims.Add(body, 1);
                    base.body.RecalculateStats();
                    Log.Debug(victims.Count + " burning enemies");
                }
                else
                {
                    victims[body]++;
                }
            }

            public void RemoveVictimBurnStack(CharacterBody body)
            {
                if (victims.ContainsKey(body))
                {
                    victims[body]--;

                    if (victims[body] <= 0)
                    {
                        victims.Remove(body);
                        base.body.RecalculateStats();
                        Log.Debug(victims.Count + " burning enemies");
                    }
                }
            }

            public void RemoveAllVictimBurnStacks(CharacterBody body)
            {
                if (victims.ContainsKey(body))
                {

                    victims.Remove(body);
                    base.body.RecalculateStats();
                    Log.Debug(victims.Count + " burning enemies");

                }
            }
        }
    }
}