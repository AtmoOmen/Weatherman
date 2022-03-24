﻿using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace Weatherman
{
    unsafe class Weatherman : IDalamudPlugin
    {
        internal const int SecondsInDay = 60 * 60 * 24;
        internal static double ETMult = 144D / 7D;
        internal static bool Init = false;

        public string Name => "Weatherman";
        internal MemoryManager memoryManager;
        internal OrchestrionController orchestrionController;
        internal Gui ConfigGui;
        internal byte WeatherTestActive = 255;
        internal Dictionary<ushort, TerritoryType> zones;
        internal HashSet<ushort> weatherAllowedZones = new()
        {
            128, 129, //limsa lominsa
            132, 133, //gridania
            130, 131, //uldah
            628, //kugane
            418, 419, //ishgard
            819, //crys
            820, //eulmore
            962, //sharla
            963, //dragoncity
        };
        internal HashSet<ushort> timeAllowedZones = new()
        {
            163, 367, //qarn
            158, 362, //brayfox
            623, //bardam

        };
        internal Dictionary<byte, string> weathers;
        internal ExcelSheet<WeatherRate> weatherRates;
        internal Dictionary<ushort, ZoneSettings> ZoneSettings;
        internal ClockOffNag clockOffNag;
        internal Configuration configuration;
        internal byte SelectedWeather = 255;
        internal byte UnblacklistedWeather = 0;
        internal bool PausePlugin = false;
        internal Stopwatch stopwatch;
        internal long totalTime = 0;
        internal long totalTicks = 0;
        internal bool profiling = false;
        internal bool InCutscene = false;

        internal bool TimeOverride = false;
        internal int TimeOverrideValue = 0;

        public void Dispose()
        {
            configuration.Save();
            Svc.Framework.Update -= HandleFrameworkUpdate;
            Svc.ClientState.Logout -= StopSongIfModified;
            Svc.PluginInterface.UiBuilder.Draw -= ConfigGui.Draw;
            Svc.ClientState.TerritoryChanged -= HandleZoneChange;
            Svc.Commands.RemoveHandler("/weatherman");
            memoryManager.Dispose();
            clockOffNag.Dispose();
            StopSongIfModified();
            orchestrionController.Dispose();
        }

        public Weatherman(DalamudPluginInterface pluginInterface)
        {
            pluginInterface.Create<Svc>();
            new TickScheduler(delegate { 
                stopwatch = new();
                orchestrionController = new(this);
                memoryManager = new(this);
                zones = Svc.Data.GetExcelSheet<TerritoryType>().ToDictionary(row => (ushort)row.RowId, row => row);
                weatherAllowedZones.UnionWith(Svc.Data.GetExcelSheet<TerritoryType>().Where(x => x.Mount).Select(x => (ushort)x.RowId));
                timeAllowedZones.UnionWith(weatherAllowedZones);
                timeAllowedZones.UnionWith(Svc.Data.GetExcelSheet<TerritoryType>().Where(x => x.QuestBattle.Value.RowId != 0).Select(x => (ushort)x.RowId));
                weathers = Svc.Data.GetExcelSheet<Weather>().ToDictionary(row => (byte)row.RowId, row => row.Name.ToString());
                weatherRates = Svc.Data.GetExcelSheet<WeatherRate>();
                ZoneSettings = new();
                foreach (var z in zones)
                {
                    var s = new ZoneSettings
                    {
                        ZoneId = z.Key,
                        ZoneName = z.Value.PlaceName.Value.Name,
                        terr = z.Value
                    };
                    s.Init(this);
                    ZoneSettings.Add(s.ZoneId, s);
                }
                configuration = pluginInterface.GetPluginConfig() as Configuration ?? new();
                configuration.Initialize(this);
                var normalweathers = new HashSet<byte>();
                foreach (var z in ZoneSettings)
                {
                    foreach (var a in z.Value.SupportedWeathers)
                    {
                        if (a.IsNormal)
                        {
                            normalweathers.Add(a.Id);
                        }
                    }
                }
                var tempdict = new Dictionary<byte, bool>(configuration.BlacklistedWeathers);
                foreach (var i in tempdict)
                {
                    if (!normalweathers.Contains(i.Key))
                    {
                        configuration.BlacklistedWeathers.Remove(i.Key);
                    }
                }
                foreach (var i in normalweathers)
                {
                    if (!configuration.BlacklistedWeathers.ContainsKey(i)) configuration.BlacklistedWeathers.Add(i, false);
                }
                Svc.Framework.Update += HandleFrameworkUpdate;
                ConfigGui = new(this);
                Svc.PluginInterface.UiBuilder.Draw += ConfigGui.Draw;
                Svc.PluginInterface.UiBuilder.OpenConfigUi += delegate { ConfigGui.configOpen = true; };
                Svc.ClientState.TerritoryChanged += HandleZoneChange;
                ApplyWeatherChanges(Svc.ClientState.TerritoryType);
                Svc.Commands.AddHandler("/weatherman", new CommandInfo(delegate { ConfigGui.configOpen = true; }) { HelpMessage = "Open plugin settings" });
                if(ChlogGui.ChlogVersion > configuration.ChlogReadVer)
                {
                    new ChlogGui(this);
                }
                Svc.ClientState.Logout += StopSongIfModified;
                clockOffNag = new(this);
                Init = true;
            }, Svc.Framework);
        }

        private void HandleZoneChange(object s, ushort u)
        {
            PluginLog.Debug("Zone changed to " + u + "; time mod allowed="+CanModifyTime() + ", weather mod allowed="+CanModifyWeather());
            ApplyWeatherChanges(u);
        }

        void StopSongIfModified(object _ = null, object __ = null)
        {
            if (orchestrionController.BGMModified)
            {
                orchestrionController.StopSong();
                orchestrionController.BGMModified = false;
            }
        }

        public void ApplyWeatherChanges(ushort u)
        {
            try
            {
                PluginLog.Debug("Applying weather changes");
                TimeOverride = false;
                SelectedWeather = 255;
                UnblacklistedWeather = 0;
                StopSongIfModified();
                if (ZoneSettings.ContainsKey(u))
                {
                    var z = ZoneSettings[u];
                    if (configuration.MusicEnabled && z.Music != 0 && !orchestrionController.BGMModified)
                    {
                        orchestrionController.PlaySong(z.Music);
                        orchestrionController.BGMModified = true;
                    }
                    if (z.WeatherControl)
                    {
                        var weathers = new List<byte>();
                        foreach (var v in z.SupportedWeathers)
                        {
                            if (v.Selected) weathers.Add(v.Id);
                        }
                        if (weathers.Count > 0)
                        {
                            SelectedWeather = weathers[new Random().Next(0, weathers.Count)];
                        }
                        else
                        {
                            
                        }
                    }
                    else
                    {
                        var unblacklistedWeatherCandidates = new List<byte>();
                        foreach (var v in z.SupportedWeathers)
                        {
                            if (configuration.BlacklistedWeathers.ContainsKey(v.Id)
                                && !configuration.BlacklistedWeathers[v.Id]
                                && IsWeatherNormal(v.Id, Svc.ClientState.TerritoryType))
                            {
                                unblacklistedWeatherCandidates.Add(v.Id);
                            }
                        }
                        if (unblacklistedWeatherCandidates.Count > 0)
                        {
                            UnblacklistedWeather =
                                 unblacklistedWeatherCandidates[new Random().Next(0, unblacklistedWeatherCandidates.Count)];
                        }
                    }
                }
                PluginLog.Debug("Selected weather:" + SelectedWeather + "; unblacklisted weather: " + UnblacklistedWeather);
                
            }
            catch(Exception e)
            {
                PluginLog.Error($"{e.Message}\n{e.StackTrace ?? ""}");
            }
        }

        void HandleFrameworkUpdate(Framework f)
        {
            try
            {
                if (profiling)
                {
                    totalTicks++;
                    stopwatch.Restart();
                }
                if (Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Svc.Condition[ConditionFlag.WatchingCutscene78])
                {
                    if (!InCutscene)
                    {
                        PluginLog.Debug("Cutscene started");
                        InCutscene = true;
                        if (configuration.DisableInCutscene)
                        {
                            StopSongIfModified();
                        }
                    }
                }
                else
                {
                    if (InCutscene)
                    {
                        PluginLog.Debug("Cutscene ended");
                        InCutscene = false;
                        ApplyWeatherChanges(Svc.ClientState.TerritoryType);
                    }
                }
                if (Svc.ClientState.LocalPlayer != null
                    && !PausePlugin
                    && !(configuration.DisableInCutscene && InCutscene))
                {
                    if (CanModifyTime())
                    {
                        SetTimeBySetting(GetZoneTimeFlowSetting(Svc.ClientState.TerritoryType));
                    }
                    else
                    {
                        memoryManager.DisableCustomTime();
                    }
                    if (CanModifyWeather())
                    {
                        if (SelectedWeather != 255)
                        {
                            memoryManager.EnableCustomWeather();
                            if (memoryManager.GetWeather() != SelectedWeather)
                            {
                                memoryManager.SetWeather(SelectedWeather);
                                if (configuration.DisplayNotifications)
                                {
                                    Svc.PluginInterface.UiBuilder.AddNotification($"{weathers[SelectedWeather]}\nReason: selected by user", "Weatherman: weather changed", NotificationType.Info, 5000);
                                }
                            }
                        }
                        else
                        {
                            var suggesterWeather = *memoryManager.TrueWeather;
                            if (UnblacklistedWeather != 0 && suggesterWeather != UnblacklistedWeather
                            && configuration.BlacklistedWeathers.ContainsKey(suggesterWeather)
                            && configuration.BlacklistedWeathers[suggesterWeather])
                            {
                                suggesterWeather = UnblacklistedWeather;
                            }
                            //this is to retain smooth transitions
                            if (suggesterWeather == *memoryManager.TrueWeather)
                            {
                                memoryManager.DisableCustomWeather();
                            }
                            else
                            {
                                memoryManager.EnableCustomWeather();
                                if (memoryManager.GetWeather() != suggesterWeather)
                                {
                                    memoryManager.SetWeather(suggesterWeather);
                                    if (configuration.DisplayNotifications)
                                    {
                                        Svc.PluginInterface.UiBuilder.AddNotification($"{weathers[SelectedWeather]}\nReason: found blacklisted weather", "Weatherman: weather changed", NotificationType.Info, 5000);
                                    }
                                }
                            }

                        }
                    }
                    else
                    {
                        memoryManager.DisableCustomWeather();
                    }

                }
                else
                {
                    memoryManager.DisableCustomTime();
                    memoryManager.DisableCustomWeather();
                }
                if (profiling)
                {
                    stopwatch.Stop();
                    totalTime += stopwatch.ElapsedTicks;
                }
            }
            catch (Exception e)
            {
                PluginLog.Error($"{e.Message}\n{e.StackTrace ?? ""}");
            }
        }

        internal bool CanModifyTime()
        {
            return configuration.EnableTimeControl && timeAllowedZones.Contains(Svc.ClientState.TerritoryType);
        }

        internal bool CanModifyWeather()
        {
            return configuration.EnableWeatherControl && weatherAllowedZones.Contains(Svc.ClientState.TerritoryType);
        }

        void SetTimeBySetting(int setting)
        {
            if (TimeOverride)
            {
                memoryManager.EnableCustomTime();
                memoryManager.SetTime((uint)TimeOverrideValue);
            }
            else
            {
                if (setting == 0) //game managed
                {
                    memoryManager.DisableCustomTime();
                }
                else if (setting == 1) //normal
                {
                    memoryManager.EnableCustomTime();
                    var et = GetET();
                    memoryManager.SetTime((uint)(et % SecondsInDay));
                }
                else if (setting == 2) //fixed
                {
                    memoryManager.EnableCustomTime();
                    uint et = (uint)GetZoneTimeFixedSetting(Svc.ClientState.TerritoryType);
                    memoryManager.SetTime(et);
                }
                else if (setting == 3) //infiniday
                {
                    memoryManager.EnableCustomTime();
                    var et = GetET();
                    var timeOfDay = et % SecondsInDay;
                    if (timeOfDay > 18 * 60 * 60 || timeOfDay < 6 * 60 * 60) et += SecondsInDay / 2;
                    memoryManager.SetTime((uint)(et % SecondsInDay));
                }
                else if (setting == 4) //infiniday r
                {
                    memoryManager.EnableCustomTime();
                    var et = GetET();
                    var timeOfDay = et % SecondsInDay;
                    if (timeOfDay > 18 * 60 * 60) et -= 2 * (timeOfDay - 18 * 60 * 60);
                    if (timeOfDay < 6 * 60 * 60) et += 2 * (6 * 60 * 60 - timeOfDay);
                    memoryManager.SetTime((uint)(et % SecondsInDay));
                }
                else if (setting == 5) //infininight
                {
                    memoryManager.EnableCustomTime();
                    var et = GetET();
                    var timeOfDay = et % SecondsInDay;
                    if (timeOfDay < 18 * 60 * 60 && timeOfDay > 6 * 60 * 60) et += SecondsInDay / 2;
                    memoryManager.SetTime((uint)(et % SecondsInDay));
                }
                else if (setting == 6) //infininight r
                {
                    memoryManager.EnableCustomTime();
                    var et = GetET();
                    var timeOfDay = et % SecondsInDay;
                    if (timeOfDay < 18 * 60 * 60 && timeOfDay > 6 * 60 * 60) et -= 2 * (timeOfDay - 6 * 60 * 60);
                    memoryManager.SetTime((uint)(et % SecondsInDay));
                }
                else if (setting == 7) //real world
                {
                    memoryManager.EnableCustomTime();
                    var now = DateTimeOffset.Now;
                    var et = (now + now.Offset).ToUnixTimeSeconds();
                    memoryManager.SetTime((uint)(et % SecondsInDay));
                }
            }
        }

        int GetZoneTimeFlowSetting(ushort terr)
        {
            if (ZoneSettings.ContainsKey(terr))
            {
                if (ZoneSettings[terr].TimeFlow > 0) return ZoneSettings[terr].TimeFlow;
            }
            return configuration.GlobalTimeFlowControl;
        }

        int GetZoneTimeFixedSetting(ushort terr)
        {
            if (ZoneSettings.ContainsKey(terr))
            {
                if (ZoneSettings[terr].TimeFlow == 2) return ZoneSettings[terr].FixedTime;
            }
            return configuration.GlobalFixedTime;
        }

        public bool IsWeatherNormal(byte id, ushort terr)
        {
            foreach (var u in weatherRates.GetRow(zones[terr].WeatherRate).UnkData0)
            {
                if (u.Weather != 0 && u.Weather == id) return true; 
            }
            return false;
        }

        public List<byte> GetWeathers(ushort id) //from titleedit https://github.com/lmcintyre/TitleEditPlugin
        {
            var weathers = new List<byte>();
            if (!zones.TryGetValue(id, out var path)) return null;
            try
            {
                var file = Svc.Data.GetFile<LvbFile>($"bg/{path.Bg}.lvb");
                if (file?.weatherIds == null || file.weatherIds.Length == 0)
                    return null;
                foreach (var weather in file.weatherIds)
                    if (weather > 0 && weather < 255)
                        weathers.Add((byte)weather);
                weathers.Sort();
                return weathers;
            }
            catch (Exception e)
            {
                PluginLog.Error(e, $"Failed to load lvb for {path}");
            }
            return null;
        }

        internal long GetET()
        {
            return (long)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * ETMult / 1000D);
        }
    }
}
