﻿using R2API;
using RoR2;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static BraqsItems.Util.Helpers;

namespace BraqsItems
{
    public class HealFromBleedVoid
    {
        public static ItemDef itemDef;
        private static GameObject healBurstPrefab;

        internal static void Init()
        {
            if (!ConfigManager.HealFromBleed_isEnabled.Value) return;

            Log.Info("Initializing Tick Queen Item");

            //ITEM//
            itemDef = GetItemDef("HealFromBleedVoid");

            ItemDisplayRuleDict displayRules = new ItemDisplayRuleDict(null);
            ItemAPI.Add(new CustomItem(itemDef, displayRules));

            //EFFECTS//
            healBurstPrefab = generateEffects();
            ContentAddition.AddEffect(healBurstPrefab);

            Hooks();

            Log.Info("Tick Queen Initialized");
        }

        private static GameObject generateEffects()
        {
            Color voidHeal = new Color(87.1f, 67.8f, 98f);

            Gradient gradient = new Gradient();
            gradient.mode = GradientMode.Blend;
            gradient.alphaKeys = new GradientAlphaKey[]
            {
                new GradientAlphaKey{alpha = 181f, time = 0f },
                new GradientAlphaKey{alpha = 0f, time = 1f}
            };
            gradient.colorKeys = new GradientColorKey[]
            {
                new GradientColorKey{color = voidHeal, time = 0f},
                new GradientColorKey{color = new Color(0f, 0f, 0f), time = 1f},
            };           

            GameObject prefab = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/EliteEarth/AffixEarthHealExplosion.prefab").WaitForCompletion().InstantiateClone("VoidHealPulse");
            Texture nullifierOffsetColorRamp = Addressables.LoadAssetAsync<Texture>("RoR2/Base/Common/ColorRamps/texRampNullifierSmooth.png").WaitForCompletion();
            //Material voidStarMat = Addressables.LoadAssetAsync<Material>("RoR2/Base/Nullifier/matNullifierStarParticle.mat").WaitForCompletion();
            GameObject mushroomEffect = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/MushroomVoid/MushroomVoidEffect.prefab").WaitForCompletion();
            var voidStars = mushroomEffect.transform.Find("Visual/Stars").gameObject.GetComponent<ParticleSystem>();
            var voidStarsRenderer = mushroomEffect.transform.Find("Visual/Stars").gameObject.GetComponent<ParticleSystemRenderer>();


            Object.Destroy(prefab.transform.Find("Flash, White").gameObject);
            Object.Destroy(prefab.transform.Find("Flash, Blue").gameObject);

            var chunks = prefab.transform.Find("Chunks").gameObject.GetComponent<ParticleSystemRenderer>();
            var chunksMaterial = chunks.material;
            chunksMaterial.SetTexture("_RemapTex", nullifierOffsetColorRamp);

            var light = prefab.transform.Find("Point Light").gameObject.GetComponent<Light>();
            light.color = voidHeal;
            light.range = 1f;

            var nova = prefab.transform.Find("Nova Sphere").gameObject.GetComponent<ParticleSystemRenderer>();
            var novaMaterial = nova.material;
            novaMaterial.SetTexture("_RemapTex", nullifierOffsetColorRamp);

            var lightShafts = prefab.transform.Find("LightShafts").gameObject.GetComponent<ParticleSystem>();
            var lightShaftsMain = lightShafts.main;
            lightShaftsMain.startColor = voidHeal;
            var colorOverLifetime3 = lightShafts.colorOverLifetime;
            colorOverLifetime3.color = new ParticleSystem.MinMaxGradient(gradient);

            var starsObject = prefab.transform.Find("Rapid Small FLash, Blue").gameObject.GetComponent<ParticleSystemRenderer>();
            starsObject.materials = voidStarsRenderer.materials;
            var stars = prefab.transform.Find("Rapid Small FLash, Blue").gameObject.GetComponent<ParticleSystem>();
            var starsMain = stars.main;
            starsMain.maxParticles = 100;
            starsMain.startLifetime = 0.5f;

            var starsRot = stars.rotationOverLifetime;
            starsRot.enabled = true;
            starsRot.x = voidStars.rotationOverLifetime.x;
            starsRot.y = voidStars.rotationOverLifetime.y;
            starsRot.z = voidStars.rotationOverLifetime.z;
            starsRot.xMultiplier = voidStars.rotationOverLifetime.xMultiplier;
            starsRot.yMultiplier = voidStars.rotationOverLifetime.yMultiplier;
            starsRot.zMultiplier = voidStars.rotationOverLifetime.zMultiplier;

            var starsCol = stars.colorOverLifetime;
            starsCol.enabled = true;
            starsCol.color = voidStars.colorOverLifetime.color;

            var starsSize = stars.sizeOverLifetime;
            starsSize.enabled = true;
            starsSize.size = voidStars.sizeOverLifetime.size;

            starsMain.startColor = voidStars.main.startColor;


            prefab.TryGetComponent<EffectComponent>(out EffectComponent effectComponent);
            effectComponent.applyScale = true;

            return prefab;
        }

        private static void Hooks()
        {
            On.RoR2.HealthComponent.TakeDamage += HealthComponent_TakeDamage;
        }

        //May want to move this to a different hook.
        private static void HealthComponent_TakeDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            if (!damageInfo.rejected && damageInfo.dotIndex == DotController.DotIndex.Fracture)
            {
                if ((bool)damageInfo.attacker && damageInfo.attacker.TryGetComponent(out CharacterBody attackerBody) && attackerBody.inventory)
                {
                    int stack = attackerBody.inventory.GetItemCount(itemDef);

                    if (stack > 0 && attackerBody.healthComponent)
                    {
                        HealthComponent healthComponent = attackerBody.healthComponent;

                        //heal 1% for 100% damage dealt
                        float percentHeal = ((stack - 1) * ConfigManager.HealFromBleed_percentPerStack.Value + ConfigManager.HealFromBleed_percentBase.Value) * damageInfo.damage / (attackerBody.damage);

                        FireHealBurst(percentHeal, stack, attackerBody);
                    }
                }
            }
            orig(self, damageInfo);
        }

        private static void FireHealBurst(float fractionalHeal, int stack, CharacterBody body)
        {
            if (stack <= 0)
            {
                Log.Warning("FireHealBurst called with no stacks");
                return;
            }

            float healRadius = 10f + 5f * (stack - 1);

            EffectData effectData = new EffectData
            {
                scale = healRadius,
                origin = body.corePosition,
            };
            EffectManager.SpawnEffect(healBurstPrefab, effectData,true);

            SphereSearch sphereSearch = new SphereSearch
            {
                mask = LayerIndex.entityPrecise.mask,
                origin = body.corePosition,
                queryTriggerInteraction = QueryTriggerInteraction.Collide,
                radius = healRadius
            };

            TeamMask teamMask = default(TeamMask);
            teamMask.AddTeam(body.teamComponent.teamIndex);

            List<HurtBox> hurtBoxesList = new List<HurtBox>();

            sphereSearch.RefreshCandidates().FilterCandidatesByHurtBoxTeam(teamMask).FilterCandidatesByDistinctHurtBoxEntities()
                     .GetHurtBoxes(hurtBoxesList);

            int i = 0;
            for (int count = hurtBoxesList.Count; i < count; i++)
            {
                HealthComponent target = hurtBoxesList[i].healthComponent;
                target.HealFraction(fractionalHeal, default(ProcChainMask));
                RoR2.Util.PlaySound("Play_item_proc_TPhealingNova_hitPlayer", target.gameObject);
            }
        }
    }
}