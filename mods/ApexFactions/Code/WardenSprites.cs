using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.utils;
using UnityEngine;

namespace ApexFactions
{
    [HarmonyPatch]
    public static class WardenSprites
    {
        private sealed class RoleTextures
        {
            public Texture2D[] Idle;
            public Texture2D[] Walk;
            public Texture2D[] Swim;
            public Texture2D[] Attack;
            public Texture2D[] Happy;
            public Texture2D Fallback;

            public bool HasAny =>
                Fallback != null ||
                (Idle != null && Idle.Length > 0) ||
                (Walk != null && Walk.Length > 0) ||
                (Swim != null && Swim.Length > 0) ||
                (Attack != null && Attack.Length > 0) ||
                (Happy != null && Happy.Length > 0);
        }

        private sealed class RoleSprites
        {
            public Sprite[] Idle;
            public Sprite[] Walk;
            public Sprite[] Swim;
            public Sprite[] Attack;
            public Sprite[] Happy;
            public Sprite Fallback;

            public bool HasAny =>
                Fallback != null ||
                (Idle != null && Idle.Length > 0) ||
                (Walk != null && Walk.Length > 0) ||
                (Swim != null && Swim.Length > 0) ||
                (Attack != null && Attack.Length > 0) ||
                (Happy != null && Happy.Length > 0);
        }

        private static readonly AccessTools.FieldRef<Actor, bool> IsForcedSocializeIconRef;
        private static readonly AccessTools.FieldRef<Actor, double> TimestampSocialRef;
        private static readonly AccessTools.FieldRef<Actor, double> ForcedSocialTimestampRef;
        private static readonly AccessTools.FieldRef<Actor, float> AttackTimerRef;
        private static readonly AccessTools.FieldRef<Actor, double> LastAttackTimestampRef;
        private static readonly AccessTools.FieldRef<BaseSimObject, BaseStats> StatsRef;
        private static readonly Func<Actor, bool> IsAffectedByLiquidFn;

        private static RoleTextures _citizenTex;
        private static RoleTextures _warriorTex;
        private static RoleTextures _leaderTex;

        private static RoleSprites _citizen;
        private static RoleSprites _warrior;
        private static RoleSprites _leader;

        private static Sprite _iconSprite;
        private static bool _loaded;
        private static bool _hasAnyTexture;
        private static bool _calibrated;

        static WardenSprites()
        {
            try { IsForcedSocializeIconRef = AccessTools.FieldRefAccess<Actor, bool>("is_forced_socialize_icon"); } catch { }
            try { TimestampSocialRef = AccessTools.FieldRefAccess<Actor, double>("timestamp_tween_session_social"); } catch { }
            try { ForcedSocialTimestampRef = AccessTools.FieldRefAccess<Actor, double>("is_forced_socialize_timestamp"); } catch { }
            try { AttackTimerRef = AccessTools.FieldRefAccess<Actor, float>("attack_timer"); } catch { }
            try { LastAttackTimestampRef = AccessTools.FieldRefAccess<Actor, double>("last_attack_timestamp"); } catch { }
            try { StatsRef = AccessTools.FieldRefAccess<BaseSimObject, BaseStats>("stats"); } catch { }

            try
            {
                MethodInfo m = AccessTools.Method(typeof(Actor), "isAffectedByLiquid");
                if (m != null)
                {
                    IsAffectedByLiquidFn = (Func<Actor, bool>)Delegate.CreateDelegate(typeof(Func<Actor, bool>), m);
                }
            }
            catch { }
        }

        public static Sprite IconSprite => _iconSprite;
        public static bool HasCustomSprites => _hasAnyTexture;

        public static void Load(string modFolder)
        {
            if (_loaded || string.IsNullOrEmpty(modFolder)) return;
            _loaded = true;

            string dir = Path.Combine(modFolder, "GameResources", "wardens");

            _iconSprite = LoadIconSafe(Path.Combine(dir, "warden_icon.png"));

            _citizenTex = new RoleTextures
            {
                Idle = LoadFrames(dir, "citizen_idle", 2),
                Walk = LoadFrames(dir, "citizen_walk", 4),
                Swim = LoadFrames(dir, "citizen_swim", 2),
                Happy = LoadFrames(dir, "citizen_happy", 2),
                Fallback = LoadTextureSafe(Path.Combine(dir, "warden_citizen.png"))
            };

            _warriorTex = new RoleTextures
            {
                Idle = LoadFrames(dir, "warrior_idle", 2),
                Walk = LoadFrames(dir, "warrior_walk", 4),
                Swim = LoadFrames(dir, "warrior_swim", 2),
                Attack = LoadFrames(dir, "warrior_attack", 2),
                Happy = LoadFrames(dir, "warrior_happy", 2),
                Fallback = LoadTextureSafe(Path.Combine(dir, "warden_warrior.png"))
            };

            _leaderTex = new RoleTextures
            {
                Idle = LoadFrames(dir, "leader_idle", 2),
                Walk = LoadFrames(dir, "leader_walk", 4),
                Swim = LoadFrames(dir, "leader_swim", 2),
                Attack = LoadFrames(dir, "leader_attack", 2),
                Happy = LoadFrames(dir, "leader_happy", 2),
                Fallback = LoadTextureSafe(Path.Combine(dir, "warden_leader.png"))
            };

            _hasAnyTexture = _citizenTex.HasAny || _warriorTex.HasAny || _leaderTex.HasAny;

            int frameCount = CountRoleFrames(_citizenTex) + CountRoleFrames(_warriorTex) + CountRoleFrames(_leaderTex);
            Debug.Log($"[ApexFactions] Warden textures loaded (frames={frameCount}, fallbacks={(_citizenTex.Fallback != null ? 1 : 0) + (_warriorTex.Fallback != null ? 1 : 0) + (_leaderTex.Fallback != null ? 1 : 0)}, icon={_iconSprite != null}).");
        }

        private static int CountRoleFrames(RoleTextures rt)
        {
            if (rt == null) return 0;
            return (rt.Idle?.Length ?? 0)
                 + (rt.Walk?.Length ?? 0)
                 + (rt.Swim?.Length ?? 0)
                 + (rt.Attack?.Length ?? 0)
                 + (rt.Happy?.Length ?? 0);
        }

        private static Texture2D[] LoadFrames(string dir, string prefix, int count)
        {
            var list = new List<Texture2D>(count);
            for (int i = 0; i < count; i++)
            {
                string path = Path.Combine(dir, $"{prefix}_{i}.png");
                Texture2D tex = LoadTextureSafe(path);
                if (tex != null)
                {
                    list.Add(tex);
                }
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        private static Texture2D LoadTextureSafe(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes == null || bytes.Length == 0) return null;

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };

                if (!ImageConversion.LoadImage(tex, bytes))
                {
                    UnityEngine.Object.Destroy(tex);
                    return null;
                }
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApexFactions] Failed to load texture '{path}': {e.Message}");
                return null;
            }
        }

        private static Sprite LoadIconSafe(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return SpriteLoadUtils.LoadSingleSprite(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApexFactions] Failed to load icon sprite '{path}': {e.Message}");
                return null;
            }
        }

        private static void BuildAllSprites(float targetPpu)
        {
            _citizen = BuildRoleSprites(_citizenTex, targetPpu);
            _warrior = BuildRoleSprites(_warriorTex, targetPpu);
            _leader  = BuildRoleSprites(_leaderTex, targetPpu);
        }

        private static RoleSprites BuildRoleSprites(RoleTextures tex, float ppu)
        {
            if (tex == null) return new RoleSprites();
            return new RoleSprites
            {
                Idle = CreateSpriteArray(tex.Idle, ppu),
                Walk = CreateSpriteArray(tex.Walk, ppu),
                Swim = CreateSpriteArray(tex.Swim, ppu),
                Attack = CreateSpriteArray(tex.Attack, ppu),
                Happy = CreateSpriteArray(tex.Happy, ppu),
                Fallback = CreateSprite(tex.Fallback, ppu)
            };
        }

        private static Sprite[] CreateSpriteArray(Texture2D[] textures, float ppu)
        {
            if (textures == null || textures.Length == 0) return null;
            var arr = new Sprite[textures.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                arr[i] = CreateSprite(textures[i], ppu);
            }
            return arr;
        }

        private static Sprite CreateSprite(Texture2D tex, float ppu)
        {
            if (tex == null) return null;
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.09f),
                ppu,
                0,
                SpriteMeshType.FullRect
            );
        }

        private static Sprite PickFrame(Sprite[] frames, float t, float fps, Sprite fallback)
        {
            if (frames == null || frames.Length == 0) return fallback;
            int len = frames.Length;
            int idx = (int)Mathf.Floor(t * fps);
            idx = ((idx % len) + len) % len;
            return frames[idx] ?? fallback;
        }

        private static bool IsSwimming(Actor actor)
        {
            if (IsAffectedByLiquidFn != null)
                return IsAffectedByLiquidFn(actor);
            return actor.isTouchingLiquid();
        }

        private static bool IsAttacking(Actor actor)
        {
            if (actor.isFighting()) return true;
            if (AttackTimerRef != null && AttackTimerRef(actor) > 0f) return true;
            if (LastAttackTimestampRef != null)
            {
                var world = MapBox.instance;
                double ts = LastAttackTimestampRef(actor);
                if (world != null && ts > 0.0 && world.getWorldTimeElapsedSince(ts) < 0.65f)
                    return true;
            }
            return false;
        }

        private static bool IsSocializing(Actor actor)
        {
            if (IsForcedSocializeIconRef != null && IsForcedSocializeIconRef(actor))
                return true;

            var world = MapBox.instance;
            if (world == null) return false;

            if (TimestampSocialRef != null)
            {
                double socialTs = TimestampSocialRef(actor);
                if (socialTs > 0.0)
                {
                    double diff = world.getCurSessionTime() - socialTs;
                    if (diff > -2.5 && diff < 2.5)
                        return true;
                }
            }

            if (ForcedSocialTimestampRef != null)
            {
                double forcedTs = ForcedSocialTimestampRef(actor);
                if (forcedTs > 0.0 && world.getWorldTimeElapsedSince(forcedTs) < 2.5f)
                    return true;
            }

            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Actor), "calculateMainSprite")]
        private static void CalculateMainSpritePostfix(Actor __instance, ref Sprite __result)
        {
            if (__instance == null || __instance.asset == null) return;
            if (__instance.asset.id != WardenSpeciesRegistry.WardenAssetId) return;
            if (!_hasAnyTexture) return;

            try
            {
                if (!_calibrated)
                {
                    if (__result == null) return;
                    float vanillaPpu = __result.pixelsPerUnit;
                    float vanillaH = __result.rect.height;
                    float targetPpu = Mathf.Max(0.1f, vanillaPpu * (25f / Mathf.Max(8f, vanillaH)));
                    BuildAllSprites(targetPpu);
                    _calibrated = true;
                    Debug.Log($"[ApexFactions] Calibrated Warden sprite PPU={targetPpu:0.00} (vanilla PPU={vanillaPpu:0.00}, vanillaH={vanillaH:0.0}).");
                }

                RoleSprites role = null;
                if ((__instance.isKing() || __instance.isCityLeader()) && _leader != null && _leader.HasAny)
                {
                    role = _leader;
                }
                else
                {
                    bool inRetaliation = __instance.kingdom != null && WardenSystem.HasActiveRetaliation(__instance.kingdom.id);
                    if ((__instance.isWarrior() || inRetaliation) && _warrior != null && _warrior.HasAny)
                    {
                        role = _warrior;
                    }
                    else if (_citizen != null && _citizen.HasAny)
                    {
                        role = _citizen;
                    }
                }

                if (role == null)
                {
                    role = (_citizen != null && _citizen.HasAny) ? _citizen
                         : (_warrior != null && _warrior.HasAny) ? _warrior
                         : _leader;
                    if (role == null || !role.HasAny) return;
                }

                Sprite baseFallback = role.Fallback
                    ?? (role.Idle != null && role.Idle.Length > 0 ? role.Idle[0] : null)
                    ?? (_citizen != null ? _citizen.Fallback : null);

                if (!__instance.isAlive())
                {
                    Sprite staticSprite = (role.Idle != null && role.Idle.Length > 0 ? role.Idle[0] : null) ?? baseFallback;
                    if (staticSprite != null) __result = staticSprite;
                    return;
                }

                float phase = (__instance.GetHashCode() & 0xFF) * 0.037f;
                float t = AnimationHelper.getTime() + phase;

                Sprite chosen;
                if (IsSwimming(__instance))
                {
                    chosen = PickFrame(role.Swim, t, 3.5f, PickFrame(role.Idle, t, 1.8f, baseFallback));
                }
                else if (IsAttacking(__instance))
                {
                    Sprite[] attackFrames = (role.Attack != null && role.Attack.Length > 0) ? role.Attack : role.Walk;
                    chosen = PickFrame(attackFrames, t, 6.0f, baseFallback);
                }
                else if (IsSocializing(__instance))
                {
                    chosen = PickFrame(role.Happy, t, 4.5f, PickFrame(role.Idle, t, 1.8f, baseFallback));
                }
                else if (__instance.is_moving)
                {
                    BaseStats st = StatsRef != null ? StatsRef(__instance) : null;
                    float speed = st != null ? st["speed"] : 20f;
                    float walkFps = Mathf.Clamp(7.5f + (speed - 15f) * 0.14f, 8.0f, 11.0f);
                    chosen = PickFrame(role.Walk, t, walkFps, baseFallback);
                }
                else
                {
                    chosen = PickFrame(role.Idle, t, 1.8f, baseFallback);
                }

                if (chosen != null)
                {
                    __result = chosen;
                }
            }
            catch { }
        }
    }
}
