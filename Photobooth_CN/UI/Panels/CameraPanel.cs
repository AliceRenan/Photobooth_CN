using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Photobooth.Controls;
using Photobooth.Maths;
using Photobooth.UI.Canvas;
using Photobooth.UI.Stateless;

namespace Photobooth.UI.Panels;

internal class CameraPanel(
    PortraitController portrait,
    CameraController camera,
    Configuration config
) : Panel(FontAwesomeIcon.Camera, "相机")
{
    private readonly PortraitController _portrait = portrait;
    private readonly CameraController _camera = camera;
    private readonly Configuration _config = config;

    private bool _followCharacter = false;
    private bool _compensateFoV = false;

    // The part of the body to face when facing/tracking the character.
    private const ushort Body = 6;
    private const ushort Head = 26;
    private static readonly ushort _Part = Head;

    // When rotating the whole camera setup, the pivot can get stuck on a wall.
    // We keep track of the original pivot angle so if there's another valid
    // position you can accumulate angular delta and jump to it.
    private float? _dragStartPivotAngle = null;

    public override string? Help { get; } =
        "左键: 长按图标移动相机(方块) 或者相机面向位置(三角)\n"
        + "右键: 长按任意位置移动整体相机方位\n"
        + "按住Shift移动图标会锁定相对距离 只进行旋转操作"
        + "Shift+右键会以人物为中心旋转";

    public override void Reset()
    {
        _followCharacter = false;
    }

    protected override void DrawBody()
    {
        var e = Editor.Current();
        if (!e.IsValid)
        {
            return;
        }

        var changed = false;

        // Track moving character.
        var newSubject = e.CharacterPosition(_Part);
        if (_portrait.IsAnimationStable)
        {
            _camera.SetSubjectPosition(newSubject);
            if (_followCharacter)
            {
                _camera.FaceSubject();
                changed = true;
            }
        }

        // Camera canvas area.
        changed |= CameraViewport(e);

        changed |= CameraWidgets();

        if (changed)
        {
            _camera.Save(e);
            e.SetHasChanged(true);
        }
    }

    private bool CameraWidgets()
    {
        var changed = false;

        var startX = ImGui.GetCursorPosX();
        var entireWidth = ImGui.GetContentRegionAvail().X;
        using (ImRaii.Group())
        {
            var buttonWidth = 0.3f * entireWidth;
            changed |= ResetRotationButton(new Vector2(buttonWidth, 0));
            changed |= FaceCharacterButton(new Vector2(buttonWidth, 0));
            changed |= TrackMotionCheckbox();
            changed |= FoVCompensationCheckbox();
        }

        ImGui.SameLine();
        var leftWidth = Math.Max(ImGui.GetCursorPosX(), startX + 0.3f * entireWidth);
        ImGui.SameLine(leftWidth);
        using (ImRaii.Group())
        {
            using var _ = ImRaii.ItemWidth(-float.Epsilon);

            changed |= ImageRotationSlider();
            changed |= CameraPitchSlider();
            changed |= PivotHeightSlider();
            changed |= CameraZoomSlider();
        }

        return changed;
    }

    private bool ResetRotationButton(Vector2 size)
    {
        var pressed = ImGui.Button("重置旋转角度", size);
        if (pressed)
        {
            _portrait.SetImageRotation(0);
        }

        return pressed;
    }

    private bool FaceCharacterButton(Vector2 size)
    {
        var pressed = ImGui.Button("相机朝向角色", size);
        if (pressed)
        {
            _camera.FaceSubject();
        }
        return pressed;
    }

    private bool TrackMotionCheckbox()
    {
        ImGui.Checkbox("追踪动作", ref _followCharacter);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "在角色移动时持续调整镜头朝向\n对PVP LB和一些有大范围移动的姿势很有用"
            );
        }

        // This doesn't modify the portrait (yet), so don't dirty it until or
        // unless the camera actually moves.
        return false;
    }

    private bool FoVCompensationCheckbox()
    {
        var compensate = _config.CompensateFoV;
        if (ImGui.Checkbox("FoV 调整", ref _compensateFoV))
        {
            _config.CompensateFoV = _compensateFoV;
            _config.Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "改变FOV时调整镜头前后位置\r\n"
                    + "尽量让画面中保留相同的取景范围\r\n"
                    + "如果镜头面朝着人物 效果会更精确"
            );
        }

        // This never changes the portrait so we don't want to mark it dirty.
        return false;
    }

    private bool ImageRotationSlider()
    {
        var rotation = (int)_portrait.GetImageRotation();
        var changed = ImPB.IconSliderInt(
            "##rotation",
            FontAwesomeIcon.Redo,
            ref rotation,
            -CameraConsts.RotationMax,
            CameraConsts.RotationMax,
            "旋转角度: %d°",
            "相机旋转"
        );
        if (changed)
        {
            _portrait.SetImageRotation((short)rotation);
        }
        return changed;
    }

    private bool PivotHeightSlider()
    {
        var pivotY = _camera.Pivot.Y;
        var changed = ImPB.IconSliderFloat(
            "##height",
            FontAwesomeIcon.RulerVertical,
            ref pivotY,
            CameraConsts.PivotYMin,
            CameraConsts.PivotYMax,
            "摄像机高度: %.1f",
            "相机高度"
        );
        if (changed)
        {
            _camera.SetPivotPositionY(pivotY);
        }
        return changed;
    }

    private bool CameraPitchSlider()
    {
        var pitch = -_camera.Direction.LatDegrees;
        var changed = ImPB.IconSliderFloat(
            "##pitch",
            FontAwesomeIcon.ArrowsUpDown,
            ref pitch,
            -CameraConsts.PitchMax,
            -CameraConsts.PitchMin,
            "上下角度: %+.0f°",
            "垂直角度"
        );
        if (changed)
        {
            _camera.SetCameraPitchRadians(-MathF.Tau * pitch / 360);
        }
        return changed;
    }

    private bool CameraZoomSlider()
    {
        var f = _camera.FocalLength;
        var changed = ImPB.IconSliderFloat(
            "##zoom",
            FontAwesomeIcon.SearchPlus,
            ref f,
            CameraController.FocalLengthMin,
            CameraController.FocalLengthMax,
            "镜头: %.0fmm",
            "焦距 (缩放)"
        );
        if (changed)
        {
            _camera.SetFocalLength(f, _compensateFoV);
            _portrait.SetCameraZoom(_camera.Zoom);
        }
        return changed;
    }

    private bool CameraViewport(Editor e)
    {
        var changed = false;

        var subject = _camera.Subject;
        var cameraXZ = _camera.Camera.XZ();
        var pivotXZ = _camera.Pivot.XZ();
        var subjectXZ = subject.XZ();
        var targetXZ = _camera.TargetXZ;

        using var canvas = new CameraCanvas();
        var shiftHeld = ImGui.IsKeyDown(ImGuiKey.ModShift);

        // Draggable sun for light angle.
        var lightDirection = _portrait.GetDirectionalLightDirection();
        if (canvas.DragSun(ref lightDirection))
        {
            _portrait.SetDirectionalLightDirection(lightDirection);
            changed = true;
        }

        // Camera target handle.
        var newTargetXZ = targetXZ;
        if (canvas.DragTarget(ref newTargetXZ))
        {
            _camera.SetTargetPositionXZ(newTargetXZ, shiftHeld);
            changed = true;
        }

        if (shiftHeld && (ImGeo.IsHandleHovered() || ImGeo.IsHandleActive()))
        {
            canvas.AddOrbitIndicator(targetXZ, cameraXZ);
        }

        // Camera position handle.
        var newCameraXZ = cameraXZ;
        if (canvas.DragCamera(ref newCameraXZ))
        {
            _camera.SetCameraPositionXZ(newCameraXZ, shiftHeld);
            changed = true;
        }

        if (shiftHeld && (ImGeo.IsHandleHovered() || ImGeo.IsHandleActive()))
        {
            canvas.AddOrbitIndicator(cameraXZ, targetXZ);
        }

        // Allow canvas-wide panning and rotation, if not doing something else.
        if (!changed)
        {
            changed |= PanOrRotate(canvas);
        }

        // Mousewheel zoom.
        if (ImGui.IsItemHovered())
        {
            var wheelDelta = ImGui.GetIO().MouseWheel;
            if (wheelDelta != 0)
            {
                _camera.AdjustCameraDistance(-wheelDelta);
                changed = true;
            }
        }

        // Draw things that might have changed.
        if (_config.ShowCoordinates)
        {
            canvas.AddPositionText(newCameraXZ, targetXZ);
        }

        canvas.AddCameraWedge(_camera.Camera.XZ(), _camera.Direction.LonRadians, _camera.FoV);
        canvas.AddPlayerMarker(subjectXZ, e.CharacterDirection());
        canvas.AddLightMarker(lightDirection);
        canvas.AddCameraApparatus(_camera.Camera.XZ(), _camera.Pivot.XZ(), _camera.TargetXZ);

        canvas.AddLegend();

        return changed;
    }

    /// <summary>
    /// Right click to pan, shift-right click to rotate.
    /// </summary>
    private bool PanOrRotate(CameraCanvas canvas)
    {
        var shiftHeld = ImGui.IsKeyDown(ImGuiKey.ModShift);
        var pivotXZ = _camera.Pivot.XZ();
        var subjectXZ = _camera.Subject.XZ();

        // Right click pan/rotate.
        var panning =
            ImGui.IsItemActive()
            && ImGui.IsMouseDown(ImGuiMouseButton.Right)
            && !ImGeo.IsAnyHandleActive();

        var mouseDelta = ImGeo.ScaleToView(ImGui.GetIO().MouseDelta);
        var mouseThreshold = mouseDelta.LengthSquared() > 1e-6;
        if (panning && shiftHeld)
        {
            canvas.AddOrbitIndicator(pivotXZ, subjectXZ);
            _dragStartPivotAngle ??= (subjectXZ - pivotXZ).Atan2();
        }
        else
        {
            _dragStartPivotAngle = null;
        }

        if (panning && mouseThreshold)
        {
            if (shiftHeld)
            {
                var newMouse = ImGeo.MouseViewPos();
                var oldMouse =
                    newMouse - ImGeo.ScaleToView(ImGui.GetMouseDragDelta(ImGuiMouseButton.Right));

                var pivotAngle = (subjectXZ - pivotXZ).Atan2();
                var oldPivotAngle = _dragStartPivotAngle ?? pivotAngle;
                var oldMouseAngle = (subjectXZ - oldMouse).Atan2();
                var newAngle = (subjectXZ - newMouse).Atan2();

                var theta = newAngle - oldMouseAngle + oldPivotAngle - pivotAngle;
                _camera.RotateEverything(theta);
            }
            else
            {
                _camera.Translate(mouseDelta.InsertY(0));
            }
            return true;
        }

        return false;
    }
}
