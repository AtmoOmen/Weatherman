namespace Weatherman
{
    internal partial class Gui
    {
        void DrawTabGlobal()
        {
            ImGui.TextUnformatted("全局控制: ");
            ImGui.SameLine();
            HelpMarker("无 - 游戏控制时间;\n" +
                "正常 - 插件控制时间, 时间正常流逝;\n" +
                "锁定 - 时间被锁定为指定值\n"
                + "永昼 - 白天结束时, 会从清晨开始另一个白天\n"
                + "永昼 (反) - 白天结束时, 会从黄昏开始倒回清晨\n"
                + "(永夜/永夜 (反) 的表现和上一致)");
            ImGui.PushItemWidth(150f);
            ImGui.Combo("##timecomboglobal", ref p.configuration.GlobalTimeFlowControl, timeflowcombo, timeflowcombo.Length);
            ImGui.PopItemWidth();
            if (p.configuration.GlobalTimeFlowControl == 2)
            {
                ImGui.TextUnformatted("设置想要的时间(秒), 双击可手动输入");
                ImGui.PushItemWidth(150f);
                ImGui.DragInt("##timecontrolfixedglobal", ref p.configuration.GlobalFixedTime, 100.0f, 0, Weatherman.SecondsInDay - 1);
                if (p.configuration.GlobalFixedTime > Weatherman.SecondsInDay
                    || p.configuration.GlobalFixedTime < 0) p.configuration.GlobalFixedTime = 0;
                ImGui.PopItemWidth();
                ImGui.SameLine();
                ImGui.TextUnformatted(DateTimeOffset.FromUnixTimeSeconds(p.configuration.GlobalFixedTime).ToString("HH:mm:ss"));
            }
            if(p.configuration.GlobalTimeFlowControl == 7)
            {
                if (ImGui.RadioButton("使用本机时间", !p.configuration.UseGMTForRealTime)) p.configuration.UseGMTForRealTime = false;
                if (ImGui.RadioButton("使用服务器时间 (格林尼治时间)", p.configuration.UseGMTForRealTime)) p.configuration.UseGMTForRealTime = true;
                ImGui.SetNextItemWidth(150);
                ImGui.InputInt("附加时间偏移 (时) ", ref p.configuration.Offset);
                if (p.configuration.Offset < -12) p.configuration.Offset = -12;
                if (p.configuration.Offset > 12) p.configuration.Offset = 12;
            }
            ImGui.Checkbox("启用音乐控制", ref p.configuration.MusicEnabled);
            ImGui.TextUnformatted("需要安装并启用 Orchestrion 插件");
            ImGui.Checkbox("过场剧情期间禁用插件", ref p.configuration.DisableInCutscene);
            ImGui.Checkbox("启用时间控制", ref p.configuration.EnableTimeControl);
            ImGui.Checkbox("启用天气控制", ref p.configuration.EnableWeatherControl);
            ImGui.Checkbox("禁用时钟同步检测", ref p.configuration.NoClockNag);
            ImGui.Checkbox("修改时间流速", ref p.configuration.ChangeTimeFlowSpeed);
            if (p.configuration.ChangeTimeFlowSpeed)
            {
                ImGui.SetNextItemWidth(100f);
                ImGui.DragFloat("时间流速倍率", ref p.configuration.TimeFlowSpeed, 0.01f, 0f, 100f);
                ValidateRange(ref p.configuration.TimeFlowSpeed, 0f, 1000f);
            }
            if(ImGui.Checkbox("始终在集体动作中显示插件界面", ref p.configuration.DisplayInGpose))
            {
                Svc.PluginInterface.UiBuilder.DisableGposeUiHide = p.configuration.DisplayInGpose;
            }
        }
    }
}
