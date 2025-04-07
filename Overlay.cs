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
        GWL_EXSTYLE = -20
    }
    
    private enum WindowStyles : int
    {
        WS_EX_LAYERED = 0x80000,
        WS_EX_TRANSPARENT = 0x20
    }
    private readonly Vector2 _screenRect;
    private bool _showSettings = true;
    private bool _hideSettingsOnDisable;
    private int _playerTeam = 1;
    private int _targetTeam = 1;
    private bool _periodicMeasure;
    private float _distance;
    private Vector2? _testStartPos;
    private readonly OpenCvManager _openCvManager;
    private Task _prevRecognizeTask;
    
    private Vector2? _playerPos;
    private Vector2? _targetPos;

    private bool KeyJustPressed(KeyEnum key)
    {
        return _prevKeyStates.TryGetValue(key, out var prevUState) &&
               (prevUState & 0x8000) == 0 && (_keyStates[key] & 0x8000) != 0;
    }

    private bool IsDown(KeyEnum key)
    {
        return (_keyStates[KeyEnum.J] & 0x8000) != 0;
    }
    
    public PubgOverlayRenderer(bool hideSettingOnDisable) : base(1920, 1080)
    {
        _hideSettingsOnDisable = hideSettingOnDisable;
        _screenRect = new Vector2(1920, 1080);
        _openCvManager = new OpenCvManager();
        _prevRecognizeTask = Task.CompletedTask;
        FPSLimit = 144;
    }

    protected override Task PostInitialized()
    {
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
        
        if (_showSettings | !_hideSettingsOnDisable)
        {
            ImGui.PushStyleColor(ImGuiCol.WindowBg, _showSettings ? new Vector4(0.3f, 0.2f, 0.3f, 0.0f) : new Vector4(0.3f, 0.2f, 0.3f, 0.4f) );
            if (_showSettings) ImGui.SetNextWindowSize(new Vector2(225, 110));
            var exFlags = ImGuiWindowFlags.NoDecoration;
            ImGui.Begin("Settings", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNavFocus | exFlags);
            ImGui.Text("按下J键以手动测量距离");
            ImGui.Text("按U键切换窗口交互");ImGui.SameLine();
            ImGui.Checkbox("自动(L)", ref _periodicMeasure);
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
            
            if (ImGui.Button("大地图开始识别(K)") || KeyJustPressed(KeyEnum.K))
            {
                if (!_prevRecognizeTask.IsCompleted)
                {
                    _prevRecognizeTask.Wait();
                }
                var result = _openCvManager.GetDistance(_playerTeam, _targetTeam, true);
                
                if (result.HasValue)
                {
                    _distance = (float)result.Value.distance / 1.08f;
                    _playerPos = result.Value.playerPos;
                    _targetPos = result.Value.targetPos;
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


        if (_periodicMeasure)
        {
            _timer++;
        }
        
        if (_timer > 80)
        {
            _timer = 0;
            if (!_prevRecognizeTask.IsCompleted)
            {
                return;
            }
            _prevRecognizeTask = Task.Run(() => 
            {
                try {
                    var result = _openCvManager.GetDistance(_playerTeam, _targetTeam);
                    if (result.HasValue)
                    {
                        lock (this) // 同步访问共享变量
                        {
                            _distance = ((float)result.Value.distance / 1.08f) / 0.6f;
                            var mapPos = OpenCvManager.MapPos;
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
                } catch (Exception e) {
                    Console.WriteLine(e);
                }
            });
        }
        if (_playerPos.HasValue)
        {
            var size = _openCvManager.PlayerTemplateSize(_playerTeam);
            drawList.AddRect(_playerPos.Value - size * new Vector2(0.5f, 1), _playerPos.Value + size * new Vector2(0.5f, 0f),GetColor(_playerTeam));
        }
        if (_targetPos.HasValue)
        {
            var size = _openCvManager.TargetTemplateSize(_targetTeam);
            drawList.AddRect(_targetPos.Value - size * new Vector2(0.5f, 1), _targetPos.Value + size * new Vector2(0.5f, 0f),GetColor(_targetTeam));
        }

        // _distance = 530;
        if (_distance > 100)
        {
            drawList.AddLine(new Vector2(_screenRect.X / 2, 0), new Vector2(_screenRect.X / 2, _screenRect.Y), GetColor(_playerTeam), 1);
            drawList.AddLine(new Vector2(_screenRect.X / 2, _screenRect.Y / 2), new Vector2(_screenRect.X / 2 + 15, _screenRect.Y / 2), GetColor(_playerTeam), 1);
            drawList.AddText(new Vector2(_screenRect.X / 2 + 18, _screenRect.Y / 2 - 10), GetColor(_playerTeam), $"{_distance:F2}");
            drawList.AddText(new Vector2(_screenRect.X / 2 - 48, _screenRect.Y / 2 - 10), GetColor(_playerTeam), "水平");
            for (var i = 2; i <= 25; i++)
            {
                drawList.AddLine(new Vector2(_screenRect.X / 2, _screenRect.Y / 2 - i * 21), new Vector2(_screenRect.X / 2 + 15, _screenRect.Y / 2 - i * 21), GetColor(_playerTeam), 1);
                drawList.AddText(new Vector2(_screenRect.X / 2 + 24, _screenRect.Y / 2 - 7 - i * 21), GetColor(_playerTeam), PubgUtils.MortarDistance(PubgUtils.MortarAngle(_distance, PubgUtils.AngleToHeight(_distance, PubgUtils.IndexToAngle(i)))).ToString(CultureInfo.CurrentCulture));
            }
            for (var j = 2; j <= 25; j++)
            {
                drawList.AddLine(new Vector2(_screenRect.X / 2, _screenRect.Y / 2 + j * 21), new Vector2(_screenRect.X / 2 + 15, _screenRect.Y / 2 + j * 21), GetColor(_playerTeam), 1);
                drawList.AddText(new Vector2(_screenRect.X / 2 + 24, _screenRect.Y / 2 - 7 + j * 21), GetColor(_playerTeam), PubgUtils.MortarDistance(PubgUtils.MortarAngle(_distance, -PubgUtils.AngleToHeight(_distance, PubgUtils.IndexToAngle(j)))).ToString(CultureInfo.CurrentCulture));
            }
        }
        // else
        // {
        //     _playerPos = null;
        //     _targetPos = null;
        // }
        
        if (KeyJustPressed(KeyEnum.U))
        {
            Console.WriteLine($"showSettings: {_showSettings}");
            _showSettings = !_showSettings;
        }
        var clickable = (WindowExStyles)GetWindowLong(window.Handle, (int)WindowLongParam.GWL_EXSTYLE);
        var notClickable = clickable | WindowExStyles.WS_EX_LAYERED | WindowExStyles.WS_EX_TRANSPARENT;
        SetWindowLongPtr(window.Handle, (int)WindowLongPtr.GWL_EXSTYLE, _showSettings ? (int)notClickable : (int)clickable);

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
        
        // 检测L键下降沿 (刚刚按下)
        if (KeyJustPressed(KeyEnum.L))
        {
            _periodicMeasure = !_periodicMeasure;
        }
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