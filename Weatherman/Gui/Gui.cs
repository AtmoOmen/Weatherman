using ECommons.ImGuiMethods;

namespace Weatherman
{
    unsafe partial class Gui
    {
        private Weatherman p;
        private int curW = 0;
        private Vector4 colorGreen = new(0,1,0,1);
        internal bool configOpen = false;
        static string[] timeflowcombo = new string[] { "无", "正常", "锁定", "永昼", "永昼 (反)", "永夜", "永夜 (反)", "现实时间" };
        bool configWasOpen = false;
        int uid = 0;
        string filter = "";
        string musicFilter = "";

        public Gui(Weatherman p)
        {
            this.p = p;
        }

        public void Draw()
        {
            try
            {
                if (!configOpen)
                {
                    if (configWasOpen)
                    {
                        p.configuration.Save();
                        PluginLog.Debug("配置已保存");
                    }
                    configWasOpen = false;
                    return;
                }
                uid = 0;
                configWasOpen = true;
                if (!p.configuration.ConfigurationString.Equals(p.configuration.GetConfigurationString()))
                {
                    p.configuration.Save();
                    PluginLog.Debug("配置已保存");
                }
                ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(900, 350));
                if (ImGui.Begin("Weatherman 2.0", ref configOpen))
                {
                    KoFiButton.DrawRight();
                    ImGui.BeginTabBar("weatherman_settings");
                    if (ImGui.BeginTabItem("快速控制"))
                    {
                        DrawTabQuickControl();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("全局设置"))
                    {
                        DrawTabGlobal();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("区域设置"))
                    {
                        DrawTabZone();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("屏蔽天气"))
                    {
                        DrawTabBlacklist();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Debug"))
                    {
                        DrawTabDebug();
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
                ImGui.PopStyleVar();
                ImGui.End();
            }
            catch(Exception e)
            {
                PluginLog.Error($"{e.Message}\n{e.StackTrace ?? ""}");
            }
        }

        static void HelpMarker(string desc)
        {
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted(desc);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }
    }
}
