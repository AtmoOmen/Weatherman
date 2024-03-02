namespace Weatherman
{
    internal unsafe partial class Gui
    {
        void DrawTabBlacklist()
        {
            ImGui.BeginChild("##wblacklist");
            ImGui.TextUnformatted("请选择你希望屏蔽的天气");
            ImGui.TextUnformatted("区域设置的优先级高于此处");
            ImGui.TextUnformatted("将随机选取正常情况下能够出现的其他天气以替代被屏蔽的天气");
            ImGui.TextUnformatted("如若没有可供替代的天气, 则维持原天气");
            ImGui.TextColored(colorGreen, "区域内正常情况下可出现的天气将会标为绿色");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 1, 0, 1), "当前天气会被标为黄色 (正常)");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "或红色 (异常)");
            ImGui.Separator();
            if (ImGui.Button("应用"))
            {
                p.ApplyWeatherChanges(Svc.ClientState.TerritoryType);
            }
            ImGui.SameLine();
            ImGui.TextUnformatted("点击此按钮或更改你的区域使设置生效");
            ImGui.Separator();

            var temparr = p.configuration.BlacklistedWeathers.ToDictionary(entry => entry.Key, entry => entry.Value);
            foreach (var w in temparr)
            {
                var v = temparr[w.Key];
                var normal = p.IsWeatherNormal(w.Key, Svc.ClientState.TerritoryType);
                var current = *p.memoryManager.TrueWeather == w.Key;
                if (normal || current) ImGui.PushStyleColor(ImGuiCol.Text, current ? (normal ? new Vector4(1, 1, 0, 1) : new Vector4(1, 0, 0, 1)) : colorGreen);
                ImGui.Checkbox(w.Key + " / " + p.weathers[w.Key], ref v);
                if (normal || current) ImGui.PopStyleColor();
                p.configuration.BlacklistedWeathers[w.Key] = v;
            }
            ImGui.EndChild();
        }
    }
}
