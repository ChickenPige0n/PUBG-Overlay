using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using ClickableTransparentOverlay.Win32;
namespace PubgOverlay;

using ClickableTransparentOverlay;
using System.Threading.Tasks;
using ImGuiNET;

internal class PubgOverlayRenderer : Overlay
{
    [DllImport("user32.dll")]
    public static extern short GetKeyState(KeyEnum vKeyEnum);
    
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    
    private enum WindowLongPtr
    {
        GwlExstyle = -20
    }
    private bool _showSettings = true;
    // ReSharper disable once NotAccessedField.Local
    private bool _hideSettingsOnDisable;
    private int _playerTeam = 1;
    private int _targetTeam = 1;
    private float _distance;
    private Vector2? _testStartPos;
    private readonly OpenCvManager _openCvManager;
    private Task _recognizeTask;
    
    private Vector2? _playerPos;
    private Vector2? _targetPos;

    private bool KeyJustPressed(KeyEnum key)
    {
        return _prevKeyStates.TryGetValue(key, out var prevUState) &&
               (prevUState & 0x8000) == 0 && (_keyStates[key] & 0x8000) != 0;
    }

    private bool IsDown(KeyEnum key)
    {
        return (_keyStates[key] & 0x8000) != 0;
    }
    private readonly string? _updateUrl;
    
    public PubgOverlayRenderer(bool hideSettingOnDisable, string? updateUrl = null) : base(1920, 1080)
    {
        if (!string.IsNullOrEmpty(updateUrl))
        {
            _updateUrl = updateUrl;
        }
        _hideSettingsOnDisable = hideSettingOnDisable;
        _openCvManager = new OpenCvManager();
        _recognizeTask = Task.CompletedTask;
        FPSLimit = 144;
    }

    protected override Task PostInitialized()
    {
        var screenSize = ScreenReader.GetDisplaySize();
        Size = new Size(screenSize.width, screenSize.height);
        return Task.CompletedTask;
    }

    private int _timer;
    private readonly Dictionary<KeyEnum, short> _keyStates = new();
    private Dictionary<KeyEnum, short> _prevKeyStates = new();

    protected override void Render()
    {
        /* get key states */
        // 需要监听的按键列表
        KeyEnum[] keysToTrack = [KeyEnum.J, KeyEnum.K, KeyEnum.L, KeyEnum.U]; // J, K, L, U 键
        
        // 记录上一帧的按键状态
        _prevKeyStates = new Dictionary<KeyEnum, short>(_keyStates);
        
        // 更新当前帧按键状态
        foreach (var key in keysToTrack)
        {
            _keyStates[key] = GetKeyState(key);
        }
        var drawList = ImGui.GetForegroundDrawList();
        
        if (_showSettings)
        {
            ImGui.PushStyleColor(ImGuiCol.WindowBg, _showSettings ? new Vector4(0.3f, 0.2f, 0.3f, 0.4f) : new Vector4(0.3f, 0.2f, 0.3f, 0.0f));
            ImGui.SetNextWindowSize(new Vector2(205, _updateUrl != null ? 122 : 102));
            const ImGuiWindowFlags exFlags = ImGuiWindowFlags.NoDecoration;
            ImGui.Begin("Settings", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNavFocus | exFlags);
            ImGui.Text("长按J键移动鼠标可手动测距");
            ImGui.Text("按U键切换窗口可见性");
            ImGui.Text($"玩家: {_playerTeam}"); ImGui.SameLine();
            if (ImGui.Button("更改##PlayerTeam"))
            {
                _playerTeam = _playerTeam == 4 ? 1 : _playerTeam + 1;
            }
            ImGui.SameLine();
            ImGui.Text($"标点: {_targetTeam}"); ImGui.SameLine();
            if (ImGui.Button("更改##TargetTeam"))
            {
                _targetTeam = _targetTeam == 4 ? 1 : _targetTeam + 1;
            }
            
            if (ImGui.Button("大地图开始识别(K)"))
            {
                QueueRecognize();
            }
            
            if (_updateUrl != null)
            {
                ImGui.Text("检测到新版本");
                ImGui.SameLine();
                if (ImGui.Button("去下载"))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _updateUrl,
                        UseShellExecute = true
                    });
                }
            }
            
            ImGui.End();
            ImGui.PopStyleColor();
        }


        ImGui.SetNextWindowPos(new Vector2(1430, 589), 0, new Vector2(0.0f, 1.0f));
        ImGui.Begin("DistanceDisplay", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.AlwaysAutoResize);
        {
            ImGui.Text($"距离: {_distance:F2} 米");
        }
        ImGui.End();
        
        
        if (_recognizeTask.IsCompleted && _timer > 80)
        {
            _timer = 0;
            QueueRecognize(true);
        }

        if (!_recognizeTask.IsCompleted)
        {
            drawList.AddLine(Vector2.Zero, new Vector2(Size.Width, 0), GetColor(_playerTeam), 3);
        }
        if (_playerPos.HasValue)
        {
            var size = _openCvManager.PlayerTemplateSize(_playerTeam - 1);
            drawList.AddRect(_playerPos.Value - size * new Vector2(0.5f, 1), _playerPos.Value + size * new Vector2(0.5f, 0f),GetColor(_playerTeam));
        }
        if (_targetPos.HasValue)
        {
            var size = _openCvManager.TargetTemplateSize(_targetTeam - 1);
            drawList.AddRect(_targetPos.Value - size * new Vector2(0.5f, 1), _targetPos.Value + size * new Vector2(0.5f, 0f),GetColor(_targetTeam));
        }

        if (_distance > 100)
        {
            drawList.AddLine(new Vector2(Size.Width / 2.0f, 0), new Vector2(Size.Width / 2.0f, Size.Height), GetColor(_playerTeam), 1);
            drawList.AddLine(new Vector2(Size.Width / 2.0f, Size.Height / 2.0f), new Vector2(Size.Width / 2.0f + 15, Size.Height / 2.0f), GetColor(_playerTeam), 1);
            drawList.AddText(new Vector2(Size.Width / 2.0f + 24, Size.Height / 2.0f - 10), GetColor(_playerTeam), $"{_distance:F2}");
            drawList.AddText(new Vector2(Size.Width / 2.0f - 48, Size.Height / 2.0f - 10), GetColor(_playerTeam), "水平");
            const int screenSegments = 24;
            for (var i = -screenSegments; i <= screenSegments; i++)
            {
                if (i == 0) continue;
                const int lineLength = 15;
                var point = new Vector2(Size.Width / 2.0f, Size.Height / 2.0f + Size.Height / 2.0f / screenSegments * i);
                drawList.AddLine(
                    point, 
                    point + new Vector2(lineLength, 0),
                    GetColor(_playerTeam), 1);
                drawList.AddText(point + new Vector2(24, -7), GetColor(_playerTeam),
                    PubgUtils.MortarDistance(
                            _distance,
                            PubgUtils.ScreenPointToAngle(-(float)i / screenSegments)
                            )
                        .ToString("F0", CultureInfo.CurrentCulture));
            }
        }
        else
        {
            _playerPos = null;
            _targetPos = null;
        }
        if (KeyJustPressed(KeyEnum.K))
            QueueRecognize();
        
        if (KeyJustPressed(KeyEnum.U))
        {
            Console.WriteLine($"showSettings: {_showSettings}");
            _showSettings = !_showSettings;
        }
        var clickable = (WindowExStyles)GetWindowLong(window.Handle, (int)WindowLongParam.GWL_EXSTYLE);
        var notClickable = clickable | WindowExStyles.WS_EX_LAYERED | WindowExStyles.WS_EX_TRANSPARENT;
        SetWindowLongPtr(window.Handle, (int)WindowLongPtr.GwlExstyle, _showSettings ? (int)clickable : (int)notClickable) ;

        if (IsDown(KeyEnum.J))
        {
            _testStartPos ??= ImGui.GetMousePos();
            drawList.AddLine(_testStartPos.Value, ImGui.GetMousePos(), 0xFFFFFFFF, 2f);
            _distance = (_testStartPos.Value - ImGui.GetMousePos()).Length() / 1.08f;
        }
        else
        {
            _testStartPos = null;
        }
    }
    private void QueueRecognize(bool smallMap = false)
    {
        _playerPos = null;
        _targetPos = null;
        _distance = 0;
        Console.WriteLine("QueueRecognize");
        if (!_recognizeTask.IsCompleted)
        {
            _recognizeTask.Wait();
        }
        
        _recognizeTask = Task.Run(() =>
        {
            try
            {
                var result = _openCvManager.GetDistance(_playerTeam - 1, _targetTeam - 1, Size, !smallMap);
                GC.Collect();
                if (result.HasValue)
                {
                    Console.WriteLine($"  playerPos: {result.Value.playerPos}");
                    Console.WriteLine($"  targetPos: {result.Value.targetPos}");
                    Console.WriteLine($"  distance: {result.Value.distance}");
                    lock (this)
                    {
                        _distance = (float)result.Value.distance / 1.08f / (smallMap ? 0.6f : 1.0f);
                        var mapPos = smallMap ? OpenCvManager.MapPos : Point.Empty;
                        _playerPos = result.Value.playerPos + new Vector2(mapPos.X, mapPos.Y);
                        _targetPos = result.Value.targetPos + new Vector2(mapPos.X, mapPos.Y);
                    }
                }
                else
                {
                    lock (this)
                    {
                        _playerPos = null;
                        _targetPos = null;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        });
    }

    // ABGR format
    private static uint GetColor(int team)
    {
        return team switch
        {
            // yellow
            1 => 0xFF00FFFF,
            // orange
            2 => 0xFF0E7BEE,
            // blue
            3 => 0xFFFFA500,
            // green
            4 => 0xFF008000,
            _ => 0xFFFFFFFF,
        };
    }
}