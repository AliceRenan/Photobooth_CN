using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Photobooth.Controls;
using Photobooth.Maths;
using Photobooth.UI.Canvas;

namespace Photobooth.UI.Panels;

internal class FacingPanel(PortraitController portrait, Configuration config)
    : Panel(FontAwesomeIcon.Eye, "头部 & 眼睛")
{
    private readonly PortraitController _portrait = portrait;
    private readonly Configuration _config = config;

    private static readonly Vector2 _EyeMin = new(-20, -12);
    private static readonly Vector2 _EyeMax = new(20, 5);

    private static readonly Vector2 _HeadMin = new(-68, -79);
    private static readonly Vector2 _HeadMax = new(68, 79);

    public override string? Help { get; } =
        "拖动或双击来改变头部或眼睛面向\n在特定姿势下不起作用";

    protected override void DrawBody()
    {
        var e = Editor.Current();
        if (!e.IsValid)
        {
            return;
        }

        using var width = ImRaii.ItemWidth(ImGui.GetContentRegionAvail().X * 0.5f - 5);

        var head = _portrait.GetHeadDirection();
        if (PickFacing("头部朝向", ref head, _HeadMax, _HeadMin, e.HeadControlType()))
        {
            _portrait.SetHeadDirection(head);
        }
        ImGui.SameLine();

        var eye = _portrait.GetEyeDirection();
        if (PickFacing("眼睛朝向", ref eye, _EyeMax, _EyeMin, e.GazeControlType()))
        {
            _portrait.SetEyeDirection(eye);
        }
    }

    private bool PickFacing(
        string label,
        ref SphereLL dir,
        Vector2 topleft,
        Vector2 bottomright,
        Editor.ControlType control
    )
    {
        using var canvas = new FacingCanvas(label, topleft, bottomright);

        if (_config.ShowCoordinates)
        {
            canvas.AddCoordinates(dir);
        }

        if (FaceControlMessage(control) is string disabledReason)
        {
            canvas.AddTopText(disabledReason);
            canvas.DummyDirection(ref dir);
            return false;
        }

        return canvas.DragDirection(ref dir);
    }

    private static string? FaceControlMessage(Editor.ControlType ty)
    {
        return ty switch
        {
            Editor.ControlType.Locked => "被当前姿势控制",
            Editor.ControlType.Free => null,
            Editor.ControlType.Camera => "面向摄像机",
            _ => "发生错误 请重试",
        };
    }
}
