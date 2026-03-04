using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using static Photobooth.Configuration;

namespace Photobooth.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration _configuration;

    private static string[] AttachmentNames =>
        ["Freely movable", "Left side", "Right side", "自动设定"];
    private static WindowAttachment?[] Attachments =>
        [null, WindowAttachment.Left, WindowAttachment.Right, WindowAttachment.Auto];

    public ConfigWindow(Plugin plugin)
        : base($"{Plugin.PluginName} 设置")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;

        AllowPinning = false;
        AllowClickthrough = false;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 150),
            MaximumSize = new Vector2(360, float.MaxValue),
        };

        _configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var autoOpen = _configuration.AutoOpenWhenEditingPortrait;
        if (ImGui.Checkbox("启动肖像编辑时自动开启插件窗口", ref autoOpen))
        {
            _configuration.AutoOpenWhenEditingPortrait = autoOpen;
            _configuration.Save();
        }

        HintText($"你也可以用这个命令手动打开插件窗口 {Plugin.CommandName}.");

        var showCoordinates = _configuration.ShowCoordinates;
        if (ImGui.Checkbox("显示坐标", ref showCoordinates))
        {
            _configuration.ShowCoordinates = showCoordinates;
            _configuration.Save();
        }

        HintText("在复刻一个肖像时很有用");

        ImGui.Spacing();

        ImGui.AlignTextToFramePadding();
        ImGui.Text("想要固定窗口位置？");
        ImGui.SameLine();

        ImGui.SetNextItemWidth(-float.Epsilon);
        var attachmentIx = Array.IndexOf(Attachments, _configuration.AttachWindow);
        if (ImGui.Combo("##attachment", ref attachmentIx, AttachmentNames, AttachmentNames.Length))
        {
            _configuration.AttachWindow = Attachments[attachmentIx];
            _configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Dummy(new(0, ImGui.GetFrameHeight() / 2));

        var w = MathF.Max(
            ImGui.GetContentRegionAvail().X / 3,
            ImGui.CalcTextSize("完成").X + 2 * ImGui.GetStyle().FramePadding.X
        );
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - w);
        if (ImGui.Button("完成", new(w, 0)))
        {
            IsOpen = false;
        }
    }

    private static void HintText(string text)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudGrey2);
        using var indent = ImRaii.PushIndent();
        ImGui.TextWrapped(text);
    }
}
