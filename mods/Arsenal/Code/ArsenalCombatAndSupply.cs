using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Arsenal
{
    [HarmonyPatch(typeof(Actor))]
    public static class ArsenalCombatPatches
    {
        public static int BulletsDeflected { get; private set; }
        public static int AegisWardsTriggered { get; private set; }
        public static int AntiGunFlankStrikes { get; private set; }

        private static readonly MethodInfo AddStatusMethod =
            AccessTools.Method(typeof(BaseSimObject), "addStatusEffect", new[] { typeof(string), typeof(float), typeof(bool) });

        private static readonly Dictionary<long, float> AegisCooldowns = new Dictionary<long, float>();

        public static EquipmentAsset GetWeaponAsset(Actor actor)
        {
            if (actor?.equipment == null) return null;
            ActorEquipmentSlot slot = actor.equipment.getSlot(EquipmentType.Weapon);
            if (slot == null || slot.isEmpty()) return null;
            return slot.getItem()?.getAsset();
        }

        public static bool IsHoldingFirearm(Actor actor)
        {
            if (actor == null) return false;
            if (actor.isWeaponFirearm()) return true;
            EquipmentAsset w = GetWeaponAsset(actor);
            return w != null && ArsenalIds.FirearmWeaponIds.Contains(w.id);
        }

        private static bool HasEquippedId(Actor actor, EquipmentType slotType, string assetId)
        {
            if (actor?.equipment == null) return false;
            ActorEquipmentSlot slot = actor.equipment.getSlot(slotType);
            if (slot == null || slot.isEmpty()) return false;
            Item item = slot.getItem();
            return item?.getAsset() != null && item.getAsset().id == assetId;
        }

        // Rock-paper-scissors combat counter-play:
        // 1. Bastion Plate deflects a large fraction of firearm damage
        // 2. Aegis Ward Amulet pops a brief shield when hit by firearms
        // 3. Shock Halberd deals bonus damage to firearm wielders caught in melee
        [HarmonyPrefix]
        [HarmonyPatch("getHit")]
        private static void ActorGetHitPrefix(Actor __instance, ref float pDamage, BaseSimObject pAttacker)
        {
            if (__instance == null || pDamage <= 0f || Main.Settings == null) return;
            Actor attacker = pAttacker as Actor;
            if (attacker == null) return;

            bool attackerHasGun = IsHoldingFirearm(attacker);

            if (attackerHasGun)
            {
                // Bastion Plate bullet deflection
                if (HasEquippedId(__instance, EquipmentType.Armor, ArsenalIds.BastionPlate))
                {
                    float reduction = Mathf.Clamp(Main.Settings.BastionBulletReduction, 0f, 0.85f);
                    pDamage *= (1f - reduction);
                    BulletsDeflected++;
                }

                // Aegis Ward Amulet reactive shield
                if (HasEquippedId(__instance, EquipmentType.Amulet, ArsenalIds.AegisAmulet))
                {
                    float now = Time.unscaledTime;
                    if (!AegisCooldowns.TryGetValue(__instance.id, out float nextReady) || now >= nextReady)
                    {
                        AegisCooldowns[__instance.id] = now + Mathf.Max(3f, Main.Settings.AegisShieldCooldown);
                        AddStatusMethod?.Invoke(__instance, new object[] { "shield", 4.0f, true });
                        pDamage *= 0.5f;
                        AegisWardsTriggered++;
                    }
                }
            }
            else if (IsHoldingFirearm(__instance))
            {
                // Shock Halberd punishes slow-reloading gunners when closing into melee
                EquipmentAsset atkWeapon = GetWeaponAsset(attacker);
                if (atkWeapon != null && atkWeapon.id == ArsenalIds.ShockHalberd)
                {
                    pDamage *= (1f + Mathf.Max(0f, Main.Settings.HalberdVsGunBonus));
                    AntiGunFlankStrikes++;
                }
            }
        }
    }

    public static class ArsenalSupplySystem
    {
        public static int TotalItemsIssued { get; private set; }

        public static void TickAutoSupply()
        {
            var world = MapBox.instance;
            var settings = Main.Settings;
            if (world?.kingdoms == null || world.units?.units_only_alive == null || settings == null) return;
            if (!settings.EnableAutoSupply) return;

            foreach (Kingdom k in world.kingdoms)
            {
                if (k == null || !k.isCiv() || !k.isAlive()) continue;
                if (settings.EraGateEnabled)
                {
                    int pop = k.getPopulationPeople();
                    int cities = k.cities != null ? k.cities.Count : 0;
                    if (pop < settings.UnlockPopulation || cities < settings.UnlockCities) continue;
                }

                // Slowly outfit a few army soldiers per cycle with balanced weapons & counters
                SupplyKingdom(k, mode: "balanced", maxSoldiers: 6);
            }
        }

        public static int SupplyKingdom(Kingdom k, string mode, int maxSoldiers)
        {
            var world = MapBox.instance;
            if (world?.units?.units_only_alive == null || world.items == null || k == null) return 0;

            var alive = world.units.units_only_alive;
            int equipped = 0;

            for (int i = 0; i < alive.Count && equipped < maxSoldiers; i++)
            {
                Actor u = alive[i];
                if (u == null || !u.isAlive() || u.kingdom != k || u.army == null) continue;
                if (u.asset != null && u.asset.is_boat) continue;
                if (u.equipment == null) continue;

                // Skip soldiers already carrying custom Arsenal gear
                EquipmentAsset curW = ArsenalCombatPatches.GetWeaponAsset(u);
                if (curW != null && curW.id.StartsWith("arsenal_")) continue;

                int roll = (int)((u.id + equipped) % 10);
                if (mode == "gunpowder")
                {
                    string gunId = roll < 4 ? ArsenalIds.Musket
                        : (roll < 7 ? ArsenalIds.Flintlock
                        : (roll < 9 ? ArsenalIds.Blunderbuss : ArsenalIds.Culverin));
                    GiveItem(world, u, gunId, k);
                    equipped++;
                }
                else if (mode == "vanguard")
                {
                    string weaponId = roll < 6 ? ArsenalIds.ShockHalberd : ArsenalIds.Warbow;
                    GiveItem(world, u, weaponId, k);
                    GiveItem(world, u, ArsenalIds.BastionPlate, k);
                    if (roll % 2 == 0) GiveItem(world, u, ArsenalIds.AegisAmulet, k);
                    equipped++;
                }
                else
                {
                    // Balanced combined-arms mix
                    if (roll < 3) GiveItem(world, u, ArsenalIds.Musket, k);
                    else if (roll < 5) GiveItem(world, u, ArsenalIds.Flintlock, k);
                    else if (roll == 5) GiveItem(world, u, ArsenalIds.Blunderbuss, k);
                    else if (roll == 6) GiveItem(world, u, ArsenalIds.Culverin, k);
                    else if (roll < 9)
                    {
                        GiveItem(world, u, ArsenalIds.ShockHalberd, k);
                        GiveItem(world, u, ArsenalIds.BastionPlate, k);
                    }
                    else
                    {
                        GiveItem(world, u, ArsenalIds.Warbow, k);
                        GiveItem(world, u, ArsenalIds.AegisAmulet, k);
                    }
                    equipped++;
                }
            }
            return equipped;
        }

        private static void GiveItem(MapBox world, Actor unit, string assetId, Kingdom k)
        {
            if (AssetManager.items == null || !AssetManager.items.has(assetId)) return;
            EquipmentAsset asset = AssetManager.items.get(assetId);
            if (asset == null) return;

            Item created = world.items.newItem(asset);
            if (created == null) return;

            unit.equipment.setItem(created, unit);
            unit.setStatsDirty();
            TotalItemsIssued++;
        }
    }
}
