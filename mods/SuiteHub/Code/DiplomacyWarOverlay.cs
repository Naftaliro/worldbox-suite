using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace SuiteHub
{
    public static class DiplomacyWarOverlay
    {
        private static bool _grandWarfareChecked = false;
        private static FieldInfo _defensePactsField;

        private static bool _rectInitialized = false;
        private static Rect _rect = new Rect(20, 76, 440, 480);
        private static Vector2 _scroll;
        private static int _subTab = 0; // 0 = Active Wars & Battles, 1 = Diplomacy Matrix

        public static void Draw(Action onClose)
        {
            if (!_rectInitialized)
            {
                _rectInitialized = true;
                float x = Screen.width > 470 ? Screen.width - 455f : 20f;
                _rect = new Rect(x, 76f, 440f, 480f);
            }
            _rect = GUI.Window(941210, _rect, id => DrawContent(onClose), "War & Diplomacy Monitor (F4)", GameUiStyle.WindowStyle);
        }

        private static void DrawContent(Action onClose)
        {
            GameUiStyle.DrawCloseButton(_rect, onClose);
            var world = MapBox.instance;
            if (world == null)
            {
                GameUiStyle.Label("Load a world to view diplomacy.");
                GameUiStyle.CaptureTooltip();
                GUI.DragWindow();
                return;
            }

            GUILayout.BeginHorizontal();
            if (GameUiStyle.ToggleButton("Wars & Battles", _subTab == 0, "Wars & Live Battles|Show ongoing wars, war causes, casualties, opinion drivers, and live skirmish hotspots with 1-click camera jump and Force Peace controls."))
                _subTab = 0;
            if (GameUiStyle.ToggleButton("Relations Matrix", _subTab == 1, "Kingdom Relations Matrix|Inspect accurate bilateral opinion scores, war status, and top diplomatic drivers between the top civilized kingdoms."))
                _subTab = 1;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(395));

            if (_subTab == 0)
            {
                DrawWarsAndHotspots(world);
            }
            else
            {
                DrawRelationsMatrix(world);
            }

            GUILayout.EndScrollView();
            GameUiStyle.CaptureTooltip();
            GUI.DragWindow();
        }

        private static void DrawWarsAndHotspots(MapBox world)
        {
            var wars = new List<War>();
            if (world.wars != null)
            {
                foreach (var w in world.wars.getActiveWars())
                {
                    if (w != null) wars.Add(w);
                }
            }

            GameUiStyle.Label($"Active Wars ({wars.Count})", "Active Wars|All ongoing wars with cause, starter, duration, army strength, opinion drivers, and quick actions.", isHeader: true);
            if (wars.Count == 0)
            {
                GameUiStyle.Subtle("The world is currently at peace.");
            }
            else
            {
                War warToEnd = null;
                foreach (War w in wars)
                {
                    Kingdom atk = w.getMainAttacker();
                    Kingdom def = w.getMainDefender();
                    string atkName = atk?.name ?? "?";
                    string defName = def?.name ?? "?";

                    long deadAtk = w.getDeadAttackers();
                    long deadDef = w.getDeadDefenders();
                    long totalDead = deadAtk + deadDef;
                    int years = w.getDuration();
                    int atkCount = w.countAttackers();
                    int defCount = w.countDefenders();
                    int atkPop = w.countAttackersPopulation();
                    int defPop = w.countDefendersPopulation();
                    int atkArmy = w.countAttackersWarriors();
                    int defArmy = w.countDefendersWarriors();

                    string cause = ResolveWarCause(w);
                    string starter = FormatWarStarter(w);
                    string drivers = (atk != null && def != null) ? GetTopOpinionFactors(world, atk, def, 3) : "";

                    string allianceNote = "";
                    if (atkCount > 1 || defCount > 1)
                    {
                        allianceNote = $" | Coalitions: {atkCount}v{defCount}";
                    }

                    string tooltip = $"{w.name}|Cause: {cause} ({years}y, {totalDead} dead)"
                        + (!string.IsNullOrEmpty(starter) ? $"\nStarted by: {starter}" : "")
                        + (!string.IsNullOrEmpty(drivers) ? $"\nOpinion drivers: {drivers}" : "")
                        + $"|Attacker: {atkPop} pop ({atkArmy} army) vs Defender: {defPop} pop ({defArmy} army)";

                    GUILayout.BeginHorizontal();
                    GameUiStyle.Label($"{w.name} ({years}y) — {totalDead} dead", tooltip, isHeader: true);

                    BattleHotspot hotspot = BattleRadarSystem.FindHotspotForWar(w);
                    string jumpTip = hotspot != null
                        ? $"Jump to Skirmish|Pan camera directly to live fighting between {hotspot.SideA} and {hotspot.SideB} ({hotspot.Hits} hits)."
                        : $"Jump to War Capital|Pan camera to {defName}'s capital city.";
                    if (GameUiStyle.Button("Jump", jumpTip, GUILayout.Width(48)))
                    {
                        JumpToWar(w);
                    }

                    if (GameUiStyle.Button("Peace", $"Force Peace|Immediately end {w.name} with a white peace treaty.|Admin action", GUILayout.Width(52)))
                    {
                        warToEnd = w;
                    }
                    GUILayout.EndHorizontal();

                    GameUiStyle.Label($"   [{cause}]{allianceNote}", tooltip);
                    GameUiStyle.Subtle($"   {atkName} ({atkPop}p / {atkArmy}w / {deadAtk}d)  vs  {defName} ({defPop}p / {defArmy}w / {deadDef}d)", tooltip);

                    if (!string.IsNullOrEmpty(starter))
                    {
                        GameUiStyle.Subtle($"   Starter: {starter}", tooltip);
                    }
                    if (!string.IsNullOrEmpty(drivers))
                    {
                        GameUiStyle.Subtle($"   Drivers: {drivers}", tooltip);
                    }
                    GUILayout.Space(5);
                }

                if (warToEnd != null && world.wars != null)
                {
                    string endedName = warToEnd.name;
                    world.wars.endWar(warToEnd, WarWinner.Peace);
                    if (warToEnd.hasEnded() || !warToEnd.isAlive())
                    {
                        Main.ShowToast($"Forced peace in {endedName}.");
                    }
                    else
                    {
                        Main.ShowToast($"Peace refused in {endedName} (Warden Retaliation active).");
                    }
                }
            }

            GUILayout.Space(8);
            int activeSkirmishes = 0;
            for (int i = 0; i < BattleRadarSystem.Hotspots.Count; i++)
            {
                if (BattleRadarSystem.Hotspots[i].Hits >= 2) activeSkirmishes++;
            }

            GameUiStyle.Label($"Live Battle Hotspots ({activeSkirmishes})", "Battle Hotspots|Silent combat telemetry tracking recent skirmishes for quick camera jumps (no map circles drawn).", isHeader: true);
            if (activeSkirmishes == 0)
            {
                GameUiStyle.Subtle("No active skirmishes detected right now.");
            }
            else
            {
                for (int i = BattleRadarSystem.Hotspots.Count - 1; i >= 0; i--)
                {
                    var h = BattleRadarSystem.Hotspots[i];
                    if (h.Hits < 2) continue;
                    GUILayout.BeginHorizontal();
                    GameUiStyle.Label($"{h.SideA} vs {h.SideB} ({h.Hits} hits)", $"Active Skirmish|{h.Hits} recent combat strikes between {h.SideA} and {h.SideB} at ({(int)h.X}, {(int)h.Y}).");
                    if (GameUiStyle.Button("Go", "Jump to Battle|Pan the camera directly to this active skirmish.", GUILayout.Width(44)))
                    {
                        JumpCameraToCoords(h.X, h.Y);
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        private static void DrawRelationsMatrix(MapBox world)
        {
            var civs = new List<Kingdom>();
            if (world.kingdoms != null)
            {
                foreach (var k in world.kingdoms)
                {
                    if (k != null && k.isCiv() && k.isAlive()) civs.Add(k);
                }
            }
            civs.Sort((a, b) => b.getPopulationPeople().CompareTo(a.getPopulationPeople()));

            GameUiStyle.Label($"Civilized Kingdoms ({civs.Count})", "Top Kingdoms|Sorted by population. Hover any relation to see the top diplomatic opinion drivers.", isHeader: true);
            if (civs.Count == 0)
            {
                GameUiStyle.Subtle("No active kingdoms yet.");
                return;
            }

            int limit = Mathf.Min(civs.Count, 8);
            for (int i = 0; i < limit; i++)
            {
                Kingdom k = civs[i];
                GUILayout.BeginHorizontal();
                GameUiStyle.Label($"{k.name} ({k.getPopulationPeople()} pop, {k.cities?.Count ?? 0} cities)", $"Kingdom Overview|{k.name} controls {k.cities?.Count ?? 0} cities with {k.getPopulationPeople()} citizens.", isHeader: true);
                if (k.capital?.getTile() != null && GameUiStyle.Button("Find", $"Find {k.name}|Center the camera on {k.name}'s capital city.", GUILayout.Width(48)))
                {
                    JumpCameraToTile(k.capital.getTile());
                }
                GUILayout.EndHorizontal();

                for (int j = i + 1; j < limit; j++)
                {
                    Kingdom other = civs[j];
                    KingdomOpinion op = world.diplomacy?.getOpinion(k, other);
                    int score = op?.total ?? 0;
                    string scoreText = score > 0 ? $"+{score}" : score.ToString();
                    string topDrivers = GetTopOpinionFactors(world, k, other, 3);

                    string status;
                    if (k.isInWarWith(other))
                    {
                        status = $"AT WAR ({scoreText})";
                    }
                    else
                    {
                        status = score <= -30
                            ? $"Hostile ({scoreText})"
                            : (score >= 30 ? $"Friendly ({scoreText})" : $"Neutral ({scoreText})");
                    }

                    string tipBody = $"Opinion between {k.name} and {other.name}: {status}.";
                    if (!string.IsNullOrEmpty(topDrivers))
                    {
                        tipBody += $"\nTop drivers: {topDrivers}";
                    }
                    GameUiStyle.Label($"   ↔ {other.name}: {status}", $"Diplomatic Standing|{tipBody}");
                }
                GUILayout.Space(4);
            }
        }

        public static string ResolveWarCause(War w)
        {
            if (w == null) return "Unknown";

            // 1. Check companion mod custom causes first (Warden Retaliation, Snake Betrayal, etc.)
            if (Main.IsCompanionLoaded("ApexFactions.Main"))
            {
                string custom = Main.InvokeCompanionAction("ApexFactions.Main", "GetWarCause", w.id);
                if (!string.IsNullOrEmpty(custom) && !custom.EndsWith("is not loaded.", StringComparison.Ordinal))
                {
                    if (HasDefensePactInvolvement(w) && custom.IndexOf("Pact", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return custom + " + Defense Pact";
                    }
                    return custom;
                }
            }

            if (Main.IsCompanionLoaded("GrandWarfare.Main"))
            {
                string gwCause = Main.InvokeCompanionAction("GrandWarfare.Main", "GetWarCause", w.id);
                if (!string.IsNullOrEmpty(gwCause) && !gwCause.EndsWith("is not loaded.", StringComparison.Ordinal))
                {
                    return gwCause;
                }
            }

            // 2. Inspect vanilla WarTypeAsset ID
            string assetId = w.getAsset()?.id ?? w.data?.war_type ?? "";
            string baseCause;
            switch (assetId)
            {
                case "normal":
                    baseCause = "Territorial Conquest";
                    break;
                case "rebellion":
                    baseCause = "City Rebellion";
                    break;
                case "inspire":
                    baseCause = "Inspired Rebellion";
                    break;
                case "spite":
                    baseCause = "Divine Spite";
                    break;
                case "whisper_of_war":
                    baseCause = "Whisper of War";
                    break;
                case "clash":
                    baseCause = "Border Clash";
                    break;
                default:
                    baseCause = string.IsNullOrEmpty(assetId) ? "Open Conflict" : HumanizeId(assetId);
                    break;
            }

            if (HasDefensePactInvolvement(w))
            {
                baseCause += " + Defense Pact";
            }

            return baseCause;
        }

        public static string ResolveWarCauseShort(War w)
        {
            return ShortenWarCause(ResolveWarCause(w));
        }

        public static string ShortenWarCause(string full)
        {
            if (string.IsNullOrEmpty(full)) return "War";

            if (full.StartsWith("Warden Retaliation", StringComparison.OrdinalIgnoreCase))
            {
                int strikeIdx = full.IndexOf("Strike #", StringComparison.OrdinalIgnoreCase);
                if (strikeIdx >= 0)
                {
                    int end = strikeIdx + 8;
                    while (end < full.Length && char.IsDigit(full[end])) end++;
                    return "Warden " + full.Substring(strikeIdx, end - strikeIdx);
                }
                return "Warden Strike";
            }
            if (full.StartsWith("Snake Betrayal", StringComparison.OrdinalIgnoreCase)) return "Snake Betrayal";
            if (full.StartsWith("Territorial Conquest", StringComparison.OrdinalIgnoreCase)) return "Conquest";
            if (full.StartsWith("City Rebellion", StringComparison.OrdinalIgnoreCase)) return "Rebellion";
            if (full.StartsWith("Inspired Rebellion", StringComparison.OrdinalIgnoreCase)) return "Inspired";
            if (full.StartsWith("Divine Spite", StringComparison.OrdinalIgnoreCase)) return "Spite";
            if (full.StartsWith("Whisper of War", StringComparison.OrdinalIgnoreCase)) return "Whisper";
            if (full.StartsWith("Border Clash", StringComparison.OrdinalIgnoreCase)) return "Border Clash";

            return full.Length > 18 ? full.Substring(0, 17) + "…" : full;
        }

        private static bool HasDefensePactInvolvement(War w)
        {
            if (w == null) return false;
            try
            {
                if (!_grandWarfareChecked)
                {
                    _grandWarfareChecked = true;
                    Type gwType = AccessTools.TypeByName("GrandWarfare.Main");
                    if (gwType != null)
                    {
                        _defensePactsField = AccessTools.Field(gwType, "DefensePacts");
                    }
                }

                if (_defensePactsField == null) return false;
                var pacts = _defensePactsField.GetValue(null) as Dictionary<long, HashSet<long>>;
                if (pacts == null || pacts.Count == 0) return false;

                Kingdom mainDef = w.getMainDefender();
                if (mainDef != null && pacts.TryGetValue(mainDef.id, out var defAllies) && defAllies != null)
                {
                    foreach (Kingdom k in w.getDefenders())
                    {
                        if (k != null && k.id != mainDef.id && defAllies.Contains(k.id))
                            return true;
                    }
                }

                Kingdom mainAtk = w.getMainAttacker();
                if (mainAtk != null && pacts.TryGetValue(mainAtk.id, out var atkAllies) && atkAllies != null)
                {
                    foreach (Kingdom k in w.getAttackers())
                    {
                        if (k != null && k.id != mainAtk.id && atkAllies.Contains(k.id))
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static string FormatWarStarter(War w)
        {
            if (w?.data == null) return "";
            string kName = w.data.started_by_kingdom_name;
            string aName = w.data.started_by_actor_name;
            if (!string.IsNullOrEmpty(kName) && !string.IsNullOrEmpty(aName))
                return $"{kName} ({aName})";
            if (!string.IsNullOrEmpty(kName)) return kName;
            if (!string.IsNullOrEmpty(aName)) return aName;
            return "";
        }

        public static string GetTopOpinionFactors(MapBox world, Kingdom k1, Kingdom k2, int maxCount)
        {
            if (world?.diplomacy == null || k1 == null || k2 == null) return "";
            try
            {
                KingdomOpinion op = world.diplomacy.getOpinion(k1, k2);
                if (op?.results == null || op.results.Count == 0) return "";

                var items = new List<KeyValuePair<OpinionAsset, int>>();
                foreach (var kv in op.results)
                {
                    if (kv.Key != null && kv.Value != 0)
                    {
                        items.Add(kv);
                    }
                }
                if (items.Count == 0) return "";

                items.Sort((a, b) =>
                {
                    int mag = Math.Abs(b.Value).CompareTo(Math.Abs(a.Value));
                    if (mag != 0) return mag;
                    return a.Value.CompareTo(b.Value);
                });

                int count = Mathf.Min(items.Count, maxCount);
                var sb = new StringBuilder();
                for (int i = 0; i < count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    var kv = items[i];
                    string label = FormatOpinionAssetLabel(kv.Key, kv.Value);
                    string valStr = kv.Value > 0 ? $"+{kv.Value}" : kv.Value.ToString();
                    sb.Append(label).Append(" (").Append(valStr).Append(')');
                }
                return sb.ToString();
            }
            catch
            {
                return "";
            }
        }

        private static string FormatOpinionAssetLabel(OpinionAsset asset, int value)
        {
            if (asset == null) return "Opinion";
            try
            {
                string key = asset.getTranslationKey(value);
                if (!string.IsNullOrEmpty(key) && LocalizedTextManager.stringExists(key))
                {
                    string text = LocalizedTextManager.getText(key);
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            catch { }

            string id = asset.id ?? "opinion";
            if (id.StartsWith("opinion_", StringComparison.OrdinalIgnoreCase))
            {
                id = id.Substring(8);
            }
            return HumanizeId(id);
        }

        private static string HumanizeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            string[] parts = id.Split('_');
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (string.IsNullOrEmpty(p)) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1) sb.Append(p.Substring(1));
            }
            return sb.ToString();
        }

        public static bool JumpToWar(War w)
        {
            if (w == null) return false;
            BattleHotspot hotspot = BattleRadarSystem.FindHotspotForWar(w);
            if (hotspot != null)
            {
                JumpCameraToCoords(hotspot.X, hotspot.Y);
                return true;
            }

            WorldTile tile = w.getMainDefender()?.capital?.getTile() ?? w.getMainAttacker()?.capital?.getTile();
            if (tile != null)
            {
                JumpCameraToTile(tile);
                return true;
            }
            return false;
        }

        public static void JumpCameraToTile(WorldTile tile)
        {
            if (tile == null) return;
            JumpCameraToCoords(tile.x, tile.y);
        }

        public static void JumpCameraToCoords(float x, float y)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 pos = cam.transform.position;
            cam.transform.position = new Vector3(x, y, pos.z);
        }
    }
}
