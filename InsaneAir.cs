using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons;
using ECommons.Configuration;
using ECommons.GameFunctions;
using ECommons.ImGuiMethods;
using Splatoon;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SplatoonScriptsOfficial.Duties.Dawntrail;

public class M10S_Insane_Air : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories => [1323];

    public override Metadata? Metadata => new(4, "Errer");

    #region 数据结构

    private enum MarkerDirection
    {
        Up,     // 左上 ↖
        Middle, // 正左 ←
        Down    // 左下 ↙
    }

    private static readonly Dictionary<uint, Vector3> PositionToCoord = new()
    {
        { 14, new Vector3(87f, 0f, 87f) },
        { 15, new Vector3(100f, 0f, 87f) },
        { 16, new Vector3(113f, 0f, 87f) },
        { 17, new Vector3(87f, 0f, 100f) },
        { 18, new Vector3(100f, 0f, 100f) },
        { 19, new Vector3(113f, 0f, 100f) },
        { 20, new Vector3(87f, 0f, 113f) },
        { 21, new Vector3(100f, 0f, 113f) },
        { 22, new Vector3(113f, 0f, 113f) },
    };

    private static readonly Dictionary<ushort, MarkerDirection> Data2ToDirection = new()
    {
        { 2, MarkerDirection.Down },
        { 32, MarkerDirection.Middle },
        { 128, MarkerDirection.Up },

        { 512, MarkerDirection.Down },
        { 2048, MarkerDirection.Middle },
        { 8192, MarkerDirection.Up },
    };

    private static readonly HashSet<ushort> FireMarkerData2 = new() { 512, 2048, 8192 };

    // NEW: label classification
    private static readonly Dictionary<ushort, string> Data2ToLabel = new()
    {
        { 2, "Proteans" },
        { 512, "Proteans" },

        { 32, "Stack" },
        { 2048, "Stack" },

        { 128, "Tank Buster" },
        { 8192, "Tank Buster" },
    };

    private readonly Dictionary<uint, bool> _isFireMarker = new();
    private readonly Dictionary<uint, (MarkerDirection direction, long triggerAt)> _activeMarkers = new();

    // NEW: label per position
    private readonly Dictionary<uint, string> _labels = new();

    #endregion

    #region 配置

    private Config C => Controller.GetConfig<Config>();

    public class Config : IEzConfig
    {
        public float ConeRadius = 25.0f;
        public int ConeAngle = 25;
        public float FillIntensity = 0.35f;
        public int DelayMs = 3000;
        public int DurationMs = 6500;
        public int TimeoutMs = 9500;
        public float LineThickness = 3.0f;
        public float CircleRadius = 8.0f;

        public uint ColorUp = 0xC8FF0000;
        public uint ColorMiddle = 0xC8FFFF00;
        public uint ColorDown = 0xC800FF00;
    }

    #endregion

    #region 生命周期方法

    public override void OnSetup()
    {
        for(uint pos = 14; pos <= 22; pos++)
        {
            for(int i = 0; i < 4; i++)
            {
                Controller.RegisterElement($"Cone_{pos}_{i}", new Element(5)
                {
                    radius = C.ConeRadius,
                    coneAngleMin = -C.ConeAngle,
                    coneAngleMax = C.ConeAngle,
                    Filled = true,
                    fillIntensity = C.FillIntensity,
                    thicc = C.LineThickness,
                    includeRotation = true,
                    FaceMe = true,
                    Enabled = false,
                });
            }

            Controller.RegisterElement($"Circle_{pos}", new Element(1)
            {
                radius = C.CircleRadius,
                refActorComparisonType = 2,
                Filled = true,
                fillIntensity = C.FillIntensity,
                thicc = C.LineThickness,
                Enabled = false,
            });

            // NEW: text element at origin
            Controller.RegisterElement($"Text_{pos}", new Element(6)
            {
                Enabled = false,
                t_fontSize = 20,
                t_align = 1, // center align
            });
        }
    }

    public override void OnMapEffect(uint position, ushort data1, ushort data2)
    {
        if(position < 14 || position > 22) return;
        if(!Data2ToDirection.TryGetValue(data2, out var direction)) return;

        _isFireMarker[position] = FireMarkerData2.Contains(data2);
        _activeMarkers[position] = (direction, Environment.TickCount64);

        if(Data2ToLabel.TryGetValue(data2, out var label))
            _labels[position] = label;
        else
            _labels[position] = "";
    }

    public override void OnUpdate()
    {
        var now = Environment.TickCount64;

        var expiredKeys = _activeMarkers
            .Where(x => now - x.Value.triggerAt > C.TimeoutMs)
            .Select(x => x.Key)
            .ToList();

        foreach(var key in expiredKeys)
        {
            _activeMarkers.Remove(key);
            _isFireMarker.Remove(key);
            _labels.Remove(key);

            for(int i = 0; i < 4; i++)
                if(Controller.TryGetElementByName($"Cone_{key}_{i}", out var cone))
                    cone.Enabled = false;

            if(Controller.TryGetElementByName($"Circle_{key}", out var circle))
                circle.Enabled = false;

            if(Controller.TryGetElementByName($"Text_{key}", out var txt))
                txt.Enabled = false;
        }

        if(_activeMarkers.Count == 0) return;

        foreach(var (position, (direction, triggerAt)) in _activeMarkers)
        {
            var elapsed = now - triggerAt;
            var isUpDirection = direction == MarkerDirection.Up;

            if(elapsed < C.DelayMs)
            {
                for(int i = 0; i < 4; i++)
                    if(Controller.TryGetElementByName($"Cone_{position}_{i}", out var cone))
                        cone.Enabled = false;

                if(Controller.TryGetElementByName($"Circle_{position}", out var c))
                    c.Enabled = false;

                if(Controller.TryGetElementByName($"Text_{position}", out var txt))
                    txt.Enabled = false;

                continue;
            }

            if(elapsed > C.DelayMs + C.DurationMs)
            {
                for(int i = 0; i < 4; i++)
                    if(Controller.TryGetElementByName($"Cone_{position}_{i}", out var cone))
                        cone.Enabled = false;

                if(Controller.TryGetElementByName($"Circle_{position}", out var c))
                    c.Enabled = false;

                if(Controller.TryGetElementByName($"Text_{position}", out var txt))
                    txt.Enabled = false;

                continue;
            }

            if(!PositionToCoord.TryGetValue(position, out var coord)) continue;

            int coneCount = direction == MarkerDirection.Down ? 4 : 1;
            uint color = direction switch
            {
                MarkerDirection.Up => C.ColorUp,
                MarkerDirection.Middle => C.ColorMiddle,
                MarkerDirection.Down => C.ColorDown,
                _ => 0xC8FFFFFF
            };

            var nearestPlayers = FakeParty.Get()
                .OrderBy(p => Vector3.Distance(coord, p.Position))
                .Take(coneCount)
                .ToList();

            if(isUpDirection)
            {
                for(int i = 0; i < 4; i++)
                    if(Controller.TryGetElementByName($"Cone_{position}_{i}", out var cone))
                        cone.Enabled = false;

                if(nearestPlayers.Count > 0 && Controller.TryGetElementByName($"Circle_{position}", out var circle))
                {
                    circle.refActorObjectID = nearestPlayers[0].EntityId;
                    circle.color = color;
                    circle.radius = C.CircleRadius;
                    circle.fillIntensity = C.FillIntensity;
                    circle.thicc = C.LineThickness;
                    circle.Enabled = true;
                }
            }
            else
            {
                if(Controller.TryGetElementByName($"Circle_{position}", out var circle))
                    circle.Enabled = false;

                for(int i = 0; i < 4; i++)
                {
                    if(Controller.TryGetElementByName($"Cone_{position}_{i}", out var cone))
                    {
                        if(i < coneCount && i < nearestPlayers.Count)
                        {
                            cone.SetRefPosition(coord);
                            cone.faceplayer = $"<{GetPlayerOrder(nearestPlayers[i])}>";
                            cone.color = color;
                            cone.radius = C.ConeRadius;
                            cone.coneAngleMin = -C.ConeAngle;
                            cone.coneAngleMax = C.ConeAngle;
                            cone.fillIntensity = C.FillIntensity;
                            cone.thicc = C.LineThickness;
                            cone.Enabled = true;
                        }
                        else
                        {
                            cone.Enabled = false;
                        }
                    }
                }
            }

            // NEW: Draw text at origin
            if(Controller.TryGetElementByName($"Text_{position}", out var txtEl) && _labels.TryGetValue(position, out var label))
            {
                txtEl.SetRefPosition(coord);
                txtEl.t_text = label;
                txtEl.Enabled = true;
            }
        }
    }

    public override void OnReset()
    {
        _activeMarkers.Clear();
        _isFireMarker.Clear();
        _labels.Clear();
        HideAllElements();
    }

    #endregion

    #region 辅助方法

    private unsafe int GetPlayerOrder(IPlayerCharacter player)
    {
        for(var i = 1; i <= 8; i++)
        {
            if((nint)FakePronoun.Resolve($"<{i}>") == player.Address)
                return i;
        }
        return 0;
    }

    private void HideAllElements()
    {
        for(uint pos = 14; pos <= 22; pos++)
        {
            for(int i = 0; i < 4; i++)
                if(Controller.TryGetElementByName($"Cone_{pos}_{i}", out var cone))
                    cone.Enabled = false;

            if(Controller.TryGetElementByName($"Circle_{pos}", out var circle))
                circle.Enabled = false;

            if(Controller.TryGetElementByName($"Text_{pos}", out var txt))
                txt.Enabled = false;
        }
    }

    #endregion

    #region 设置界面

    public override void OnSettingsDraw()
    {
        ImGui.Text("扇形参数: (Cone parameters)");

        var radius = C.ConeRadius;
        if(ImGui.SliderFloat("扇形半径 (Cone radius)", ref radius, 5f, 50f))
            C.ConeRadius = radius;

        var angle = C.ConeAngle;
        if(ImGui.SliderInt("角度 (半角) (Angle - half)", ref angle, 10, 90))
            C.ConeAngle = angle;
        ImGui.SameLine();
        ImGui.TextDisabled($"(总角度: {angle * 2}°) (Total angle)");

        var circleRadius = C.CircleRadius;
        if(ImGui.SliderFloat("圆形半径 (火标记上) (Circle radius - on fire marker)", ref circleRadius, 1f, 10f))
            C.CircleRadius = circleRadius;

        var fill = C.FillIntensity;
        if(ImGui.SliderFloat("填充透明度 (Fill opacity)", ref fill, 0.1f, 1f))
            C.FillIntensity = fill;

        var thickness = C.LineThickness;
        if(ImGui.SliderFloat("线条粗细 (Line thickness)", ref thickness, 1f, 10f))
            C.LineThickness = thickness;

        ImGui.Separator();
        ImGui.Text("时间设置: (Timing settings)");

        var delay = C.DelayMs;
        if(ImGui.SliderInt("延迟绘制(ms) (Draw delay)", ref delay, 0, 10000))
            C.DelayMs = delay;

        var duration = C.DurationMs;
        if(ImGui.SliderInt("持续时间(ms) (Duration)", ref duration, 1000, 15000))
            C.DurationMs = duration;

        var timeout = C.TimeoutMs;
        if(ImGui.SliderInt("超时清除(ms) (Timeout clear)", ref timeout, 1000, 30000))
            C.TimeoutMs = timeout;

        ImGui.Separator();
        ImGui.Text("颜色设置: (Color settings)");

        var colorUp = C.ColorUp.ToVector4();
        if(ImGui.ColorEdit4("上方向 (红) (Up - Red)", ref colorUp))
            C.ColorUp = colorUp.ToUint();

        var colorMiddle = C.ColorMiddle.ToVector4();
        if(ImGui.ColorEdit4("中方向 (黄) (Middle - Yellow)", ref colorMiddle))
            C.ColorMiddle = colorMiddle.ToUint();

        var colorDown = C.ColorDown.ToVector4();
        if(ImGui.ColorEdit4("下方向 (绿) (Down - Green)", ref colorDown))
            C.ColorDown = colorDown.ToUint();

        ImGui.Separator();

        if(ImGui.Button("保存配置 (Save config)"))
            Controller.SaveConfig();

        ImGui.SameLine();
        if(ImGui.Button("清除所有标记 (Clear all markers)"))
        {
            _activeMarkers.Clear();
            _isFireMarker.Clear();
            _labels.Clear();
            HideAllElements();
        }

        if(ImGui.CollapsingHeader("调试信息 (Debug info)"))
        {
            ImGuiEx.Text($"活跃标记数: {_activeMarkers.Count} (Active markers)");
            var now = Environment.TickCount64;
            foreach(var (pos, (dir, triggerAt)) in _activeMarkers)
            {
                var elapsed = now - triggerAt;
                var state = elapsed < C.DelayMs ? "等待中 (Waiting)" :
                           elapsed < C.DelayMs + C.DurationMs ? "绘制中 (Drawing)" : "已结束 (Finished)";
                ImGuiEx.Text($"  Position {pos}: {dir} - {state} ({elapsed}ms) Label: {_labels.GetValueOrDefault(pos, "")}");
            }
        }
    }

    #endregion
}
