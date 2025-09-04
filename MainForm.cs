using Guna.UI2.WinForms;
using Guna.UI2.WinForms.Enums;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;  // 숫자 포맷 (YOLO용 소수점)
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;           // notes.json 생성용 StringBuilder
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SmartLabelingApp
{
    public partial class MainForm : Form
    {
        #region 1) Constants & Static Data (상수/정적 데이터)
        // ---- 상단바(TopBar)
        private const int TOPBAR_H = 32;
        private const int PAD_V = 2;
        private const int PAD_H = 8;
        private const int GAP = 4;

        // ---- 우측 도크(Right Dock)
        private const int RIGHT_DOCK_W = 90;
        private const int RIGHT_DOCK_T = 1;

        private const int RIGHT_ICON_PX = 22; // 아이콘 이미지 크기(px)
        private const int RIGHT_SLOT_H = 35;  // 슬롯(버튼 높이)
        private const int RIGHT_ICON_GAP = 8; // 아이콘 간 간격(아래 margin)
        private const int RIGHT_ICON_PAD = 2; // 슬롯 안쪽 여백(padding)

        private const int RIGHT_BAR1_H = 490; // 상단 아이콘바 높이
        private const int RIGHT_BAR2_H = 400; // 라벨링 ADD 바 높이
        private const int RIGHT_BAR_GAP = 4;  // 바 사이 여백

        // 바(SAVE) + 레이아웃 보정 상수
        private const int RIGHT_BAR3_H = 90;           // 기본값 (스냅 꺼두면 이 값 사용)
        private const bool RIGHT_BAR3_SNAP_TO_VIEWER = true; // 뷰어 하단에 자동 정렬
        private const int RIGHT_BAR3_TAIL = 5;         // 끝단 미세 보정(px): +면 살짝 더 아래
        private const int RIGHT_BAR_MIN_H = 40;        // 바2/바3 최소 높이


        // === Right Bar3 action area metrics ===
        private const int ACTION3_TOP = 5;   // 바3 내부의 시작 Y (위 여백)
        private const int ACTION3_GAP = 8;   // SAVE ↔ EXPORT 간격


        // ---- 프레임(Frame)
        private const int FRAME_X = 205;
        private const int FRAME_X_OFFSET = 85;
        private const int FRAME_Y = 10;
        private const int FRAME_Y_OFFSET = 5;
        private const int FRAME_W = 800;
        private const int FRAME_H = 547;
        private const int FRAME_BORDER = 2;

        // ---- 이미지 뷰어 가로 상한 (원하는 값으로 조정)
        private const int VIEWER_MAX_W = 1602;

        // ----- Label Chip Layout Controls -----
        private const int LABEL_CHIP_MIN_W = 74;     // 칩/ADD 최소 폭

        // YoLo 형식 관련
        private bool _yoloLoadedForCurrentImage = false;
        private string _lastYoloExportRoot;  // 마지막으로 Save한 YOLO 데이터셋 루트
        private readonly Dictionary<string, Color> _classColorMap = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

        private struct LabelInfo
        {
            public string Name { get; set; }
            public Color Color { get; set; }

            public LabelInfo(string name, Color color) : this()
            {
                Name = name ?? string.Empty;
                Color = color;
            }
        }

        // ---- 이미지 확장자(정적)
        private static readonly HashSet<string> _imgExts =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" };

        #endregion

        #region 2) UI Components (컨트롤/뷰 구성요소)
        // 창 효과
        private readonly Guna2BorderlessForm _borderless;
        private readonly Guna2Elipse _elipse;
        private readonly Guna2ShadowForm _shadow;

        // 상단바
        private readonly Guna2GradientPanel _topBar;
        private Guna2ControlBox _btnMin;
        private Guna2ControlBox _btnMax;
        private Guna2ControlBox _btnClose;
        private Guna2DragControl _dragControl;
        private Guna2DragControl _dragTitle;

        // 중앙 호스트 + 캔버스 레이어/캔버스
        private Guna2Panel _canvasHost;
        private Panel _canvasLayer;
        private readonly ImageCanvas _canvas;
        // --- Add Vertex용 컨텍스트 상태 ---
        private ToolStripMenuItem _miAddVertex;    // 동적 추가되는 메뉴(폴리곤일 때만 표시)
        private System.Drawing.PointF _lastCtxImgPointImg; // 마지막 우클릭 이미지 좌표

        // 우측 도크/툴바
        private Guna2Panel _rightRail;
        private Guna2Panel _rightToolDock;   // 상단 바
        private Guna2Panel _rightToolDock2;  // 라벨링 ADD 바
        private Guna2Panel _rightToolDock3;  // ★ NEW: SAVE 바
        private FlowLayoutPanel _rightTools; // 상단 툴 컨테이너
        private FlowLayoutPanel _rightTools2;// 라벨링 툴 컨테이너
        private FlowLayoutPanel _rightTools3;// ★ NEW: SAVE 바 컨테이너

        // 우측 툴 버튼들
        private readonly Guna2Button _btnOpen;
        private readonly Guna2ImageButton _btnPointer;
        private readonly Guna2ImageButton _btnTriangle;
        private readonly Guna2ImageButton _btnBox;
        private readonly Guna2ImageButton _btnNgon;
        private readonly Guna2ImageButton _btnCircle;
        private readonly Guna2ImageButton _btnBrush;
        private readonly Guna2ImageButton _btnEraser;
        private readonly Guna2ImageButton _btnMask;
        private readonly Guna2ImageButton _btnAI;
        private readonly Guna2ImageButton _btnPolygon;
        private Guna2Button _btnAdd;
        private Guna2Button _btnSave;
        private Guna2Button _btnExport;
        private Guna2Button _btnTrain;

        // 좌측 탐색(폴더/파일)
        private Guna2Panel _leftRail;
        private Guna2Panel _leftDock;
        private TreeView _fileTree;
        #endregion

        #region 3) State & Model (상태/모델)
        private string _currentImagePath; // 현재 캔버스에 로드된 이미지의 전체 경로
        private BrushSizeWindow _brushWin;      // 브러시 크기 팝업
        private Control _brushAnchorBtn;        // 마지막 앵커 버튼(브러시/지우개)
        private int _brushDiameterPx = 18;      // 현재 브러시 지름(px)
        private string _currentFolder;          // 현재 탐색 폴더 경로

        // 라벨 추가를 위한 상태
        private LabelCreateWindow _labelWin;    // 라벨 생성 창
        private Control _labelAnchorBtn;        // 라벨 창 앵커(ADD 버튼 슬롯)
        private int _labelSeq = 1;              // 빈 이름일 때 자동 이름 부여용
        #endregion

        #region 4) Initialization & Layout (Constructor)
        public MainForm()
        {
            // ---- 기본 폼 설정
            Text = "SmartLabelingApp";
            StartPosition = FormStartPosition.Manual;
            this.WindowState = FormWindowState.Maximized; // 초기 상태 고정
            this.Load += (_, __) =>
            {
                // 여기서 다시 한 번 보정하면 Shown 전에 적용되어 깜빡임이 줄어듭니다.
                this.WindowState = FormWindowState.Maximized;
            };

            MinimumSize = new Size(900, 600);
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.None;

            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                bool isShortcut =
                    (e.Control && (e.KeyCode == Keys.A || e.KeyCode == Keys.C || e.KeyCode == Keys.V || e.KeyCode == Keys.Z)) ||
                    e.KeyCode == Keys.Delete || e.KeyCode == Keys.Escape;

                if (isShortcut && _canvas != null && !_canvas.Focused)
                    _canvas.Focus(); // 실제 처리는 ImageCanvas.OnKeyDown에서
            };

            // ---- 창 효과
            _elipse = new Guna2Elipse { BorderRadius = 2, TargetControl = this };
            _borderless = new Guna2BorderlessForm
            {
                ContainerControl = this,
                BorderRadius = 2,
                TransparentWhileDrag = true,
                ResizeForm = true
            };
            _shadow = new Guna2ShadowForm { ShadowColor = Color.Black };

            // ---- 중앙 호스트(화이트 카드)
            _canvasHost = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 0, 8, 8),
                BorderColor = Color.FromArgb(220, 224, 230),
                BorderThickness = 1,
                BorderRadius = 12,
                FillColor = Color.White
            };
            _canvasHost.ShadowDecoration.Parent = _canvasHost;
            Controls.Add(_canvasHost);

            // ---- 상단바
            _topBar = new Guna2GradientPanel
            {
                Dock = DockStyle.Top,
                Height = TOPBAR_H,
                FillColor = Color.FromArgb(120, 161, 255),
                FillColor2 = Color.FromArgb(146, 228, 255),
                Padding = new Padding(PAD_H, PAD_V, PAD_H, PAD_V)
            };
            _topBar.DoubleClick += (s, e) => ToggleMaximizeRestore();
            Controls.Add(_topBar);

            _dragControl = new Guna2DragControl
            {
                TargetControl = _topBar,
                DockIndicatorTransparencyValue = 0.6f,
                UseTransparentDrag = true
            };

            int toolEdge = TOPBAR_H - PAD_V * 2;

            var rightPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = toolEdge * 3 + GAP * 4,
                BackColor = Color.Transparent
            };
            _topBar.Controls.Add(rightPanel);

            int y = PAD_V;
            int cbEdge = toolEdge;

            _btnMin = new Guna2ControlBox
            {
                ControlBoxType = ControlBoxType.MinimizeBox,
                FillColor = Color.Transparent,
                IconColor = Color.Black,
                BorderRadius = 2,
                UseTransparentBackground = true,
                Size = new Size(cbEdge, cbEdge),
                Location = new Point(rightPanel.Width - (cbEdge * 3 + GAP * 3), y)
            };
            _btnMax = new Guna2ControlBox
            {
                ControlBoxType = ControlBoxType.MaximizeBox,
                FillColor = Color.Transparent,
                IconColor = Color.Black,
                BorderRadius = 2,
                UseTransparentBackground = true,
                Size = new Size(cbEdge, cbEdge),
                Location = new Point(rightPanel.Width - (cbEdge * 2 + GAP * 2), y)
            };
            _btnClose = new Guna2ControlBox
            {
                FillColor = Color.Transparent,
                IconColor = Color.Black,
                HoverState = { FillColor = Color.FromArgb(255, 80, 80), IconColor = Color.White },
                BorderRadius = 2,
                UseTransparentBackground = true,
                Size = new Size(cbEdge, cbEdge),
                Location = new Point(rightPanel.Width - (cbEdge + GAP), y)
            };
            rightPanel.Controls.Add(_btnMin);
            rightPanel.Controls.Add(_btnMax);
            rightPanel.Controls.Add(_btnClose);

            var lblTitle = new Label
            {
                Text = "SmartLabelingApp",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                BackColor = Color.Transparent,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 10, FontStyle.Regular)
            };
            lblTitle.DoubleClick += (s, e) => ToggleMaximizeRestore();
            _topBar.Controls.Add(lblTitle);

            _dragTitle = new Guna2DragControl
            {
                TargetControl = lblTitle,
                DockIndicatorTransparencyValue = 0.6f,
                UseTransparentDrag = true
            };

            // ---- 우측 도크
            _rightRail = new Guna2Panel { Dock = DockStyle.Right, Width = RIGHT_DOCK_W };
            _canvasHost.Controls.Add(_rightRail);

            // 상단 바
            _rightToolDock = new Guna2Panel
            {
                Dock = DockStyle.Top,
                Height = RIGHT_BAR1_H,
                Padding = new Padding(6, 8, 6, 8),
                FillColor = Color.Transparent,
                BackColor = Color.Transparent,
                BorderThickness = 2,
                BorderColor = Color.Silver,
                BorderRadius = 2
            };
            _rightTools = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            _rightToolDock.Controls.Add(_rightTools);
            _rightRail.Controls.Add(_rightToolDock);

            // 라벨링 ADD 바 (수동 배치)
            _rightToolDock2 = new Guna2Panel
            {
                Dock = DockStyle.None,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Height = RIGHT_BAR2_H,
                Padding = new Padding(6, 8, 6, 8),
                FillColor = Color.Transparent,
                BackColor = Color.Transparent,
                BorderThickness = 2,
                BorderColor = Color.Silver,
                BorderRadius = 2
            };
            _rightTools2 = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            _rightToolDock2.Controls.Add(_rightTools2);
            _rightRail.Controls.Add(_rightToolDock2);

            _rightToolDock3 = new Guna2Panel
            {
                Dock = DockStyle.None,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Height = RIGHT_BAR3_H,
                Padding = new Padding(6, 8, 6, 8),
                FillColor = Color.Transparent,
                BackColor = Color.Transparent,
                BorderThickness = 2,
                BorderColor = Color.Silver,
                BorderRadius = 2
            };
            _rightTools3 = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            _rightTools3.Padding = new Padding(0, ACTION3_TOP, 0, ACTION3_TOP);
            _rightToolDock3.Controls.Add(_rightTools3);
            _rightRail.Controls.Add(_rightToolDock3);

            int innerW3 = RIGHT_DOCK_W - _rightToolDock3.Padding.Horizontal;

            _rightToolDock3.Resize += (s, e) =>
            {
                int w3 = Math.Max(LABEL_CHIP_MIN_W, _rightToolDock3.ClientSize.Width - _rightToolDock3.Padding.Horizontal);
                if (_btnSave != null) { _btnSave.Width = w3; _btnSave.Margin = new Padding(0, 0, 0, ACTION3_GAP); }
                if (_btnExport != null) { _btnExport.Width = w3; _btnExport.Margin = new Padding(0, 0, 0, ACTION3_GAP); }
                if (_btnTrain != null) { _btnTrain.Width = w3; _btnTrain.Margin = new Padding(0, 0, 0, ACTION3_GAP); }
            };

            // 하단 바 - ADD 버튼
            int innerW2 = RIGHT_DOCK_W - _rightToolDock2.Padding.Horizontal;
            _btnAdd = new Guna2Button
            {
                Text = "ADD",
                BorderRadius = 12,
                BorderThickness = 2,
                BorderColor = Color.LightGray,
                FillColor = Color.Transparent,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                AutoSize = false,
                Size = new Size(innerW2, toolEdge),
                Margin = new Padding(0, 0, 0, 8),
                TabStop = false
            };
            _btnAdd.Click += OnAddClick;
            _btnAdd.MouseDown += (s, e) => { if (!_canvas.Focused) _canvas.Focus(); };
            _rightTools2.Controls.Add(_btnAdd);

            _btnAdd.Width = Math.Max(LABEL_CHIP_MIN_W, _rightToolDock2.ClientSize.Width - _rightToolDock2.Padding.Horizontal);
            AdjustLabelChipWidths();

            _rightToolDock2.Resize += (s, e) =>
            {
                int targetW = Math.Max(LABEL_CHIP_MIN_W, _rightToolDock2.ClientSize.Width - _rightToolDock2.Padding.Horizontal);
                _btnAdd.Width = targetW;
                AdjustLabelChipWidths();
            };

            // 상단 바 - OPEN 버튼
            int innerW = RIGHT_DOCK_W - _rightToolDock.Padding.Horizontal;
            _btnOpen = new Guna2Button
            {
                Text = "OPEN",
                BorderRadius = 12,
                BorderThickness = 2,
                BorderColor = Color.LightGray,
                FillColor = Color.Transparent,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                AutoSize = false,
                Size = new Size(innerW, toolEdge),
                Margin = new Padding(0, 0, 0, 8),
                TabStop = false
            };
            _btnOpen.Click += OnOpenClick;
            _rightTools.Controls.Add(_btnOpen);

            // ★ NEW: 세 번째 바 - SAVE 버튼 (OPEN/ADD와 동일 형상)
            _btnSave = new Guna2Button
            {
                Text = "SAVE",
                BorderRadius = 12,
                BorderThickness = 2,
                BorderColor = Color.LightGray,
                FillColor = Color.Transparent,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                AutoSize = false,
                Size = new Size(innerW3, toolEdge),
                Margin = new Padding(0, 0, 0, 8),
                TabStop = false
            };
            _btnSave.Click += OnSaveClick;
            _rightTools3.Controls.Add(_btnSave);

            _btnExport = new Guna2Button
            {
                Text = "EXPORT",
                BorderRadius = 12,
                BorderThickness = 2,
                BorderColor = Color.LightGray,
                FillColor = Color.Transparent,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                AutoSize = false,
                Size = new Size(innerW3, toolEdge),
                Margin = new Padding(0, 0, 0, 8),
                TabStop = false
            };
            _btnExport.Click += OnExportClick;
            _rightTools3.Controls.Add(_btnExport);

            _btnTrain = new Guna2Button
            {
                Text = "TRAIN",
                BorderRadius = 12,
                BorderThickness = 2,
                BorderColor = Color.LightGray,
                FillColor = Color.Transparent,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                AutoSize = false,
                Size = new Size(innerW3, toolEdge),
                Margin = new Padding(0, 0, 0, 8),
                TabStop = false
            };
            _btnTrain.Click += OnTrainClick;
            _rightTools3.Controls.Add(_btnTrain);

            // 툴 아이콘 생성
            _btnPointer = CreateToolIcon(Properties.Resources.Arrow, "Pointer", RIGHT_SLOT_H, RIGHT_ICON_PX);
            _btnCircle = CreateToolIcon(Properties.Resources.Circle, "Circle", RIGHT_SLOT_H, RIGHT_ICON_PX);
            _btnTriangle = CreateToolIcon(Properties.Resources.Triangle, "Triangle", RIGHT_SLOT_H, RIGHT_ICON_PX);
            _btnBox = CreateToolIcon(Properties.Resources.Rectangle, "Box", RIGHT_SLOT_H, RIGHT_ICON_PX);
            _btnNgon = CreateToolIcon(Properties.Resources.Ngon, "N-gon", RIGHT_SLOT_H, RIGHT_ICON_PX);
            _btnPolygon = CreateToolIcon(Properties.Resources.Polyline, "Polygon", RIGHT_SLOT_H, RIGHT_ICON_PX);
            _btnBrush = CreateToolIcon(Properties.Resources.Brush, "Brush", RIGHT_SLOT_H, RIGHT_ICON_PX);
            _btnEraser = CreateToolIcon(Properties.Resources.Eraser, "Eraser", RIGHT_SLOT_H, RIGHT_ICON_PX);
            _btnMask = CreateToolIcon(Properties.Resources.Masktoggle, "Mask", RIGHT_SLOT_H, RIGHT_ICON_PX);
            _btnAI = CreateToolIcon(Properties.Resources.AI, "AI", RIGHT_SLOT_H, RIGHT_ICON_PX);

            _btnPointer.Click += delegate { SetTool(ToolMode.Pointer, _btnPointer); };
            _btnCircle.Click += delegate { SetTool(ToolMode.Circle, _btnCircle); };
            _btnTriangle.Click += delegate
            {
                SetTool(ToolMode.Polygon, _btnTriangle);
                _canvas.SetPolygonPreset(PolygonPreset.Triangle, 0);
                if (!_canvas.Focused) _canvas.Focus();
            };
            _btnBox.Click += delegate
            {
                SetTool(ToolMode.Polygon, _btnBox);
                _canvas.SetPolygonPreset(PolygonPreset.RectBox, 0);
                if (!_canvas.Focused) _canvas.Focus();
            };
            _btnNgon.Click += (s, e) =>
            {
                int currentSides = 5;
                var polyTool = _canvas.GetTool(ToolMode.Ngon) as PolygonTool;
                if (polyTool != null && polyTool.RegularSides >= 3)
                    currentSides = polyTool.RegularSides;

                using (var dlg = new NgonSidesDialog(currentSides))
                {
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    var result = dlg.ShowDialog(this);
                    if (result != DialogResult.OK) return;
                    int sides = dlg.Sides;
                    if (sides < 3) sides = 3;
                    if (polyTool != null) polyTool.RegularSides = sides;
                }
                SetTool(ToolMode.Ngon, _btnNgon);
                if (!_canvas.Focused) _canvas.Focus();
            };
            _btnPolygon.Click += delegate
            {
                SetTool(ToolMode.Polygon, _btnPolygon);
                _canvas.SetPolygonPreset(PolygonPreset.Free, 0);
                if (!_canvas.Focused) _canvas.Focus();
            };
            _btnBrush.Click += delegate
            {
                SetTool(ToolMode.Brush, _btnBrush);
                ShowBrushWindowNear(_btnBrush.Parent != null ? _btnBrush.Parent : (Control)_btnBrush);
            };
            _btnEraser.Click += delegate
            {
                SetTool(ToolMode.Eraser, _btnEraser);
                ShowBrushWindowNear(_btnEraser.Parent != null ? _btnEraser.Parent : (Control)_btnEraser);
            };
            _btnMask.Click += delegate
            {
                SetTool(ToolMode.Mask, _btnMask);
                if (_brushWin != null && _brushWin.Visible) _brushWin.Hide();
            };
            _btnAI.Click += delegate
            {
                SetTool(ToolMode.AI, _btnAI);
                if (_brushWin != null && _brushWin.Visible) _brushWin.Hide();
            };

            // 슬롯 래핑 후 상단 바에 배치
            int innerWSlot = RIGHT_DOCK_W - _rightToolDock.Padding.Horizontal;
            var slotPointer = WrapToolSlot(_btnPointer, innerWSlot, RIGHT_SLOT_H);
            var slotCircle = WrapToolSlot(_btnCircle, innerWSlot, RIGHT_SLOT_H);
            var slotTriangle = WrapToolSlot(_btnTriangle, innerWSlot, RIGHT_SLOT_H);
            var slotBox = WrapToolSlot(_btnBox, innerWSlot, RIGHT_SLOT_H);
            var slotNgon = WrapToolSlot(_btnNgon, innerWSlot, RIGHT_SLOT_H);
            var slotPolygon = WrapToolSlot(_btnPolygon, innerWSlot, RIGHT_SLOT_H);
            var slotBrush = WrapToolSlot(_btnBrush, innerWSlot, RIGHT_SLOT_H);
            var slotEraser = WrapToolSlot(_btnEraser, innerWSlot, RIGHT_SLOT_H);
            var slotMask = WrapToolSlot(_btnMask, innerWSlot, RIGHT_SLOT_H);
            var slotAI = WrapToolSlot(_btnAI, innerWSlot, RIGHT_SLOT_H);

            _rightTools.Controls.Add(slotPointer);
            _rightTools.Controls.Add(slotCircle);
            _rightTools.Controls.Add(slotTriangle);
            _rightTools.Controls.Add(slotBox);
            _rightTools.Controls.Add(slotNgon);
            _rightTools.Controls.Add(slotPolygon);
            _rightTools.Controls.Add(slotBrush);
            _rightTools.Controls.Add(slotEraser);
            _rightTools.Controls.Add(slotMask);
            _rightTools.Controls.Add(slotAI);

            // 포커스 유지(단축키 안정)
            Action<Control> bindFocus = c =>
            {
                c.TabStop = false;
                c.MouseDown += (s, e) => { if (!_canvas.Focused) _canvas.Focus(); };
                c.Click += (s, e) => { if (!_canvas.Focused) _canvas.Focus(); };
            };
            bindFocus(_btnOpen);
            bindFocus(_btnPointer);
            bindFocus(_btnTriangle);
            bindFocus(_btnBox);
            bindFocus(_btnNgon);
            bindFocus(_btnPolygon);
            bindFocus(_btnCircle);
            bindFocus(_btnBrush);
            bindFocus(_btnEraser);
            bindFocus(_btnMask);
            bindFocus(_btnAI);
            bindFocus(_btnAdd);
            bindFocus(_btnSave);
            bindFocus(_btnExport);
            bindFocus(_btnTrain);

            // ---- 좌측 탐색(폴더/파일 트리)
            _leftRail = new Guna2Panel { Dock = DockStyle.Left, Width = 200, BackColor = Color.Transparent };
            _canvasHost.Controls.Add(_leftRail);

            _leftDock = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(6, 8, 6, 8),
                FillColor = Color.Transparent,
                BackColor = Color.Transparent,
                BorderThickness = 2,
                BorderColor = Color.Silver,
                BorderRadius = 2
            };
            var leftContent = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(0), Margin = new Padding(0) };
            _fileTree = new TreeView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                FullRowSelect = true,
                HideSelection = false,
                ShowLines = true,
                ShowPlusMinus = true,
                HotTracking = true,
                ItemHeight = 20,
                Font = new Font("Segoe UI", 9f)
            };

            _fileTree.AfterSelect += (s, e) =>
            {
                if (e.Node == null) return;
                var path = e.Node.Tag as string;
                if (string.IsNullOrEmpty(path)) return;

                if (System.IO.File.Exists(path) && IsImageFile(path))
                {
                    LoadImageAtPath(path);

                    _canvas.Focus();
                }
            };

            _fileTree.StateImageList = LabelStatusService.BuildStateImageList(LabelStatusService.BadgeStyle.Check, 16, 2);
            _fileTree.ShowNodeToolTips = true;

            leftContent.Controls.Add(_fileTree);
            _leftDock.Controls.Add(leftContent);
            _leftRail.Controls.Add(_leftDock);
            _leftRail.BringToFront();

            // ---- 프레임 + 캔버스
            _canvasLayer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            _canvasLayer.Paint += (s, e) => DrawPlaceholder(e.Graphics);
            _canvasHost.Controls.Add(_canvasLayer);

            _canvas = new ImageCanvas
            {
                TabStop = true,
                BorderStyle = BorderStyle.None,
                Parent = _canvasLayer
            };
            _canvas.SetBrushDiameter(_brushDiameterPx);
            _canvas.ToolEditBegan += () => HideBrushWindow();

            // ✅ 기본 라벨을 캔버스 생성 이후에 추가/선택
            AddDefaultLabelIfMissing();

            _canvas.MouseDown += (s, e) =>
            {
                if (!_canvas.Focused) _canvas.Focus();
                if ((_canvas.Mode == ToolMode.Brush || _canvas.Mode == ToolMode.Eraser)
                    && e.Button == MouseButtons.Left
                    && _canvas.Image != null)
                {
                    var ip = _canvas.Transform.ScreenToImage(e.Location);
                    var sz = _canvas.Transform.ImageSize;
                    bool insideImage = (ip.X >= 0 && ip.Y >= 0 && ip.X < sz.Width && ip.Y < sz.Height);
                    if (insideImage) HideBrushWindow();
                }
            };

            _canvas.ModeChanged += (mode) =>
            {
                int iconPixel = RIGHT_ICON_PX;
                HighlightTool(_btnPointer, mode == ToolMode.Pointer, iconPixel);
                HighlightTool(_btnBox, mode == ToolMode.Box, iconPixel);
                HighlightTool(_btnPolygon, mode == ToolMode.Polygon, iconPixel);
                HighlightTool(_btnNgon, mode == ToolMode.Ngon, iconPixel);
                HighlightTool(_btnCircle, mode == ToolMode.Circle, iconPixel);
                HighlightTool(_btnTriangle, mode == ToolMode.Triangle, iconPixel);
                HighlightTool(_btnBrush, mode == ToolMode.Brush, iconPixel);
                HighlightTool(_btnEraser, mode == ToolMode.Eraser, iconPixel);
                HighlightTool(_btnMask, mode == ToolMode.Mask, iconPixel);
                HighlightTool(_btnAI, mode == ToolMode.AI, iconPixel);

                if (mode == ToolMode.Brush || mode == ToolMode.Eraser)
                {
                    var anchor = (mode == ToolMode.Brush)
                        ? (_btnBrush.Parent != null ? _btnBrush.Parent : (Control)_btnBrush)
                        : (_btnEraser.Parent != null ? _btnEraser.Parent : (Control)_btnEraser);
                    ShowBrushWindowNear(anchor);
                }
                else
                {
                    HideBrushWindow();
                }

                if (!_canvas.Focused) _canvas.Focus();
                this.ActiveControl = _canvas;

                if (mode == ToolMode.Pointer && _canvas.PanMode)
                    DisablePanMode();
            };

            // ---- 이미지 컨텍스트 메뉴
            var ctxImage = new ContextMenuStrip { ShowImageMargin = false, Font = new Font("Segoe UI Emoji", 9f) };
            var miPointer = new ToolStripMenuItem("🖱 | Image Pointer");
            var miPan = new ToolStripMenuItem("✋ | Image Pan");
            var miFit = new ToolStripMenuItem("📐 | Image Fit");
            var miClear = new ToolStripMenuItem("🧹 | Clear Annotations");

            _miAddVertex = new ToolStripMenuItem("➕ | Add Vertex");
            _miAddVertex.Click += (s, e) =>
            {
                var poly = _miAddVertex.Tag as PolygonShape;
                if (poly == null || _canvas == null || _canvas.Image == null) return;

                int idx = poly.InsertVertexAtClosestEdge(_lastCtxImgPointImg);
                if (idx >= 0)
                {
                    if (_canvas.Selection != null)
                        _canvas.Selection.Set(poly);
                    _canvas.Invalidate();
                }
            };
            miPointer.Click += (s, e) =>
            {
                DisablePanMode();
                _canvas.Mode = ToolMode.Pointer;
                if (!_canvas.Focused) _canvas.Focus();
            };
            miPan.Click += (s, e) =>
            {
                if (_canvas == null || _canvas.Image == null) return;
                _canvas.Mode = ToolMode.Pointer;
                if (_canvas.Selection != null) _canvas.Selection.Clear();
                EnablePanMode();
                if (!_canvas.Focused) _canvas.Focus();
            };
            miFit.Click += (s, e) =>
            {
                if (_canvas != null && _canvas.Image != null)
                {
                    _canvas.ZoomToFit();
                    _canvas.Focus();
                }
            };
            miClear.Click += (s, e) =>
            {
                if (_canvas == null) return;
                _canvas.ClearAllShapes();
                _canvas.PanMode = false;
                _canvas.Cursor = Cursors.Default;
                _canvas.Focus();
            };

            ctxImage.Items.AddRange(new ToolStripItem[] { miPointer, miPan, miFit, new ToolStripSeparator(), miClear });
            ctxImage.Opening += (s, e) =>
            {
                bool hasImg = (_canvas != null && _canvas.Image != null);
                bool hasShapes = (_canvas != null && _canvas.HasAnyShape);
                miPointer.Enabled = hasImg;
                miPan.Enabled = hasImg;
                miFit.Enabled = hasImg;
                miClear.Enabled = hasShapes;

                // 현재 마우스 위치를 이미지 좌표로 변환
                var scrPt = _canvas.PointToClient(Control.MousePosition);
                var imgPt = _canvas.Transform.ScreenToImage(scrPt);
                _lastCtxImgPointImg = imgPt;

                // 기존에 Add Vertex가 있다면 일단 제거(항상 조건부로 다시 삽입)
                if (ctxImage.Items.Contains(_miAddVertex))
                    ctxImage.Items.Remove(_miAddVertex);
                _miAddVertex.Tag = null;

                // 마우스 아래의 최상단 폴리곤 탐색
                PolygonShape target = null;
                if (hasImg && hasShapes && _canvas.Shapes != null)
                {
                    for (int i = _canvas.Shapes.Count - 1; i >= 0; --i)
                    {
                        var poly = _canvas.Shapes[i] as PolygonShape;
                        if (poly == null) continue;
                        if (poly.PointsImg == null || poly.PointsImg.Count < 3) continue;
                        if (poly.Contains(imgPt)) { target = poly; break; }
                    }
                }

                // 폴리곤일 때만 Add Vertex 메뉴를 동적으로 삽입 (miClear 바로 앞)
                if (target != null)
                {
                    _miAddVertex.Tag = target;
                    int idxInsert = ctxImage.Items.IndexOf(miClear);
                    if (idxInsert < 0) idxInsert = ctxImage.Items.Count;
                    ctxImage.Items.Insert(idxInsert, _miAddVertex);
                }
            };
            _canvas.ContextMenuStrip = ctxImage;

            // ---- 리사이즈 연동
            _canvasLayer.Resize += (s, e) =>
            {
                UpdateViewerBounds();
                if (_canvas != null && _canvas.Image != null)
                    _canvas.ZoomToFit();
            };
            UpdateViewerBounds();

            // ---- 레이아웃 이벤트
            _canvasHost.Resize += (s, e) => UpdateSideRailsLayout();
            this.Resize += (s, e) => UpdateSideRailsLayout();
            UpdateSideRailsLayout();

            this.LocationChanged += (s, e) => RepositionBrushWindow();
            this.SizeChanged += (s, e) => RepositionBrushWindow();
            this.VisibleChanged += (s, e) => RepositionBrushWindow();

            // 초기 하이라이트
            HighlightTool(_btnPointer, true, RIGHT_ICON_PX);
        }
        #endregion

        #region 5) UI Helpers (유틸/파일/레이아웃 보조)

        private void TryBindAnnotationRootNear(string folder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
                var annotationRoot = Path.Combine(folder, "AnnotationData");
                var modelRoot = Path.Combine(annotationRoot, "Model");
                if (Directory.Exists(modelRoot) && File.Exists(Path.Combine(modelRoot, "classes.txt")))
                {
                    _lastYoloExportRoot = modelRoot;
                    LabelStatusService.SetStorageRoot(annotationRoot);
                }
            }
            catch { /* ignore */ }
        }


        private void EnsureBrushWindow()
        {
            if (_brushWin == null || _brushWin.IsDisposed)
            {
                _brushWin = new BrushSizeWindow
                {
                    StartPosition = FormStartPosition.Manual,
                    ShowInTaskbar = false,
                    TopMost = true,
                    MinimumPx = 2,
                    MaximumPx = 256
                };
                _brushWin.BrushSizeChanged += OnBrushSizeChanged;
                _brushWin.FormClosed += (s, e) => { _brushWin = null; };
            }
            _brushWin.ValuePx = _brushDiameterPx;
        }
        private void HideBrushWindow()
        {
            if (_brushWin != null && !_brushWin.IsDisposed && _brushWin.Visible)
                _brushWin.Hide();
        }
        #region 6) Event Handlers (버튼/메뉴/키/마우스)
        #endregion  // 5) UI Helpers
        private void OnBrushSizeChanged(int px)
        {
            _brushDiameterPx = px;
            if (_canvas != null) _canvas.SetBrushDiameter(_brushDiameterPx);
            if (_canvas != null && !_canvas.Focused) _canvas.Focus();
        }
        private void ShowBrushWindowNear(Control anchor)
        {
            if (anchor == null || anchor.IsDisposed) return;
            EnsureBrushWindow();
            _brushAnchorBtn = anchor;

            Control refCtrl = anchor;
            if (anchor.Parent is Guna2Panel) refCtrl = anchor.Parent;

            Point pScreen = refCtrl.PointToScreen(new Point(0, 0));
            Rectangle wa = Screen.FromControl(this).WorkingArea;

            int x = pScreen.X - _brushWin.Width - 12;
            int y = pScreen.Y + (refCtrl.Height / 2) - (_brushWin.Height / 2);

            if (x < wa.Left) x = wa.Left + 8;
            if (y < wa.Top) y = wa.Top + 8;
            if (y + _brushWin.Height > wa.Bottom) y = wa.Bottom - _brushWin.Height - 8;

            _brushWin.Location = new Point(x, y);

            if (!_brushWin.Visible) _brushWin.Show(this);
            else _brushWin.BringToFront();
        }
        private void RepositionBrushWindow()
        {
            if (_brushWin != null && _brushWin.Visible && _brushAnchorBtn != null && !_brushAnchorBtn.IsDisposed)
                ShowBrushWindowNear(_brushAnchorBtn);
        }

        // ---- 아이콘/툴 유틸
        private static Bitmap MakeNearWhiteTransparent(Image img, byte threshold = 248)
        {
            var src = new Bitmap(img);
            var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) g.DrawImage(src, Point.Empty);

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    if (c.R >= threshold && c.G >= threshold && c.B >= threshold)
                        bmp.SetPixel(x, y, Color.FromArgb(0, c));
                }
            }
            src.Dispose();
            return bmp;
        }
        private Guna2ImageButton CreateToolIcon(Image img, string tooltipText, int edge, int iconPx)
        {
            var btn = new Guna2ImageButton();
            var clean = (img == null) ? new Bitmap(1, 1, PixelFormat.Format32bppArgb) : MakeNearWhiteTransparent(img, 248);

            btn.Image = clean;
            btn.ImageSize = new Size(iconPx, iconPx);
            btn.HoverState.ImageSize = new Size(iconPx + 2, iconPx + 2);
            btn.PressedState.ImageSize = new Size(iconPx, iconPx);
            btn.UseTransparentBackground = true;
            btn.BackColor = Color.Transparent;
            btn.Size = new Size(edge, edge);
            btn.Margin = new Padding(0);
            btn.Cursor = Cursors.Hand;

            var tip = new Guna2HtmlToolTip
            {
                TitleForeColor = Color.Black,
                ForeColor = Color.Black,
                BackColor = Color.White
            };
            tip.SetToolTip(btn, tooltipText);
            return btn;
        }
        private void SetTool(ToolMode mode, Guna2ImageButton clicked)
        {
            _canvas.Mode = mode;
            if (!_canvas.Focused) _canvas.Focus();
            this.ActiveControl = _canvas;

            int iconPx = RIGHT_ICON_PX;
            HighlightTool(_btnPointer, clicked == _btnPointer, iconPx);
            HighlightTool(_btnBox, clicked == _btnBox, iconPx);
            HighlightTool(_btnPolygon, clicked == _btnPolygon, iconPx);
            HighlightTool(_btnNgon, clicked == _btnNgon, iconPx);
            HighlightTool(_btnCircle, clicked == _btnCircle, iconPx);
            HighlightTool(_btnTriangle, clicked == _btnTriangle, iconPx);
            HighlightTool(_btnBrush, clicked == _btnBrush, iconPx);
            HighlightTool(_btnEraser, clicked == _btnEraser, iconPx);
            HighlightTool(_btnMask, clicked == _btnMask, iconPx);
            HighlightTool(_btnAI, clicked == _btnAI, iconPx);

            if (clicked != _btnBrush && clicked != _btnEraser)
            {
                if (_brushWin != null && _brushWin.Visible) _brushWin.Hide();
            }
            if (mode == ToolMode.Pointer && _canvas.PanMode)
                DisablePanMode();
        }
        private void HighlightTool(Guna2ImageButton btn, bool active, int baseIconPx)
        {
            if (active)
            {
                btn.ImageSize = new Size(baseIconPx + 4, baseIconPx + 4);
                btn.HoverState.ImageSize = new Size(baseIconPx + 6, baseIconPx + 6);
            }
            else
            {
                btn.ImageSize = new Size(baseIconPx, baseIconPx);
                btn.HoverState.ImageSize = new Size(baseIconPx + 2, baseIconPx + 2);
            }

            var slot = btn.Parent as Guna2Panel;
            if (slot != null)
            {
                slot.BorderColor = active ? Color.LimeGreen : Color.Transparent;
                slot.FillColor = active ? Color.FromArgb(245, 245, 245) : Color.Transparent;
            }
        }
        private Guna2Panel WrapToolSlot(Guna2ImageButton btn, int width, int height)
        {
            var slot = new Guna2Panel
            {
                Size = new Size(width, height),
                Padding = new Padding(RIGHT_ICON_PAD),
                BorderRadius = 8,
                BorderThickness = 2,
                BorderColor = Color.Transparent,
                FillColor = Color.Transparent,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, RIGHT_ICON_GAP)
            };
            btn.Dock = DockStyle.Fill;
            slot.Controls.Add(btn);
            return slot;
        }
        private void DehighlightAllTools()
        {
            int px = RIGHT_ICON_PX;
            HighlightTool(_btnPointer, false, px);
            HighlightTool(_btnBox, false, px);
            HighlightTool(_btnPolygon, false, px);
            HighlightTool(_btnNgon, false, px);
            HighlightTool(_btnCircle, false, px);
            HighlightTool(_btnTriangle, false, px);
            HighlightTool(_btnBrush, false, px);
            HighlightTool(_btnEraser, false, px);
            HighlightTool(_btnMask, false, px);
            HighlightTool(_btnAI, false, px);
        }
        private void EnablePanMode()
        {
            if (_canvas == null || _canvas.Image == null) return;
            _canvas.PanMode = true;
            DehighlightAllTools();
            if (_canvas.Selection != null) _canvas.Selection.Clear();
            _canvas.Cursor = Cursors.Hand;
            if (!_canvas.Focused) _canvas.Focus();
        }
        private void DisablePanMode()
        {
            if (_canvas == null) return;
            _canvas.PanMode = false;
            _canvas.Cursor = Cursors.Default;
            if (_btnPointer != null) SetTool(ToolMode.Pointer, _btnPointer);
        }

        // ---- 프레임/레이아웃/렌더링
        private Rectangle GetFrameRect()
        {
            if (_canvasLayer == null)
                return new Rectangle(FRAME_X, FRAME_Y, Math.Min(FRAME_W, VIEWER_MAX_W), FRAME_H); // fallback

            int rawW = _canvasLayer.ClientSize.Width - FRAME_X - FRAME_X_OFFSET;
            int rawH = _canvasLayer.ClientSize.Height - FRAME_Y - FRAME_Y_OFFSET;

            int w = Math.Max(2 * FRAME_BORDER + 2, Math.Min(rawW, VIEWER_MAX_W)); // ★ 가로 상한 적용
            int h = Math.Max(2 * FRAME_BORDER + 2, rawH);

            return new Rectangle(FRAME_X, FRAME_Y, w, h);
        }
        private void DrawPlaceholder(Graphics g)
        {
            using (var pen = new Pen(Color.Silver, FRAME_BORDER))
            using (var br = new SolidBrush(Color.FromArgb(20, Color.Gray)))
            {
                var r = GetFrameRect();
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillRectangle(br, r);
                g.DrawRectangle(pen, r);
            }
        }
        private void UpdateViewerBounds()
        {
            if (_canvasLayer == null || _canvas == null) return;
            var r = GetFrameRect();
            var inner = Rectangle.Inflate(r, -FRAME_BORDER, -FRAME_BORDER);
            _canvas.Bounds = inner;
        }

        private void UpdateSideRailsLayout()
        {
            if (_canvasLayer == null) return;

            int topPad = Math.Max(0, FRAME_Y);

            // 오른쪽 레일
            if (_rightRail != null)
            {
                _rightRail.Padding = new Padding(0, topPad - RIGHT_DOCK_T, 0, 2);
                _rightRail.Width = RIGHT_DOCK_W;

                int clientW = _rightRail.ClientSize.Width - _rightRail.Padding.Horizontal;
                int leftX = _rightRail.Padding.Left;
                int topY = _rightRail.Padding.Top;

                // 바1 (상단)
                if (_rightToolDock != null)
                {
                    _rightToolDock.Left = leftX;
                    _rightToolDock.Top = topY;
                    _rightToolDock.Width = clientW;
                    _rightToolDock.Height = RIGHT_BAR1_H;
                }

                // 바2 (ADD)
                if (_rightToolDock2 != null)
                {
                    _rightToolDock2.Left = leftX;
                    _rightToolDock2.Width = clientW;
                    _rightToolDock2.Height = Math.Max(RIGHT_BAR_MIN_H, RIGHT_BAR2_H);
                    _rightToolDock2.Top = _rightToolDock.Bottom + RIGHT_BAR_GAP;
                }

                // 바3 (SAVE) - 기본 배치
                if (_rightToolDock3 != null)
                {
                    _rightToolDock3.Left = leftX;
                    _rightToolDock3.Width = clientW;
                    _rightToolDock3.Top = _rightToolDock2.Bottom + RIGHT_BAR_GAP;

                    int h3 = Math.Max(RIGHT_BAR_MIN_H, RIGHT_BAR3_H);

                    if (RIGHT_BAR3_SNAP_TO_VIEWER)
                    {
                        // ★ 뷰어(캔버스) 하단에 스냅: inner.Bottom = frame.Top + frame.Height - FRAME_BORDER
                        int viewerBottom = _rightRail.Padding.Top + GetFrameRect().Height - FRAME_BORDER + RIGHT_BAR3_TAIL;
                        int desired = viewerBottom - _rightToolDock3.Top;

                        // 공간이 모자라면 바2를 줄여서 확보
                        if (desired < RIGHT_BAR_MIN_H)
                        {
                            int need = RIGHT_BAR_MIN_H - desired;
                            if (_rightToolDock2 != null && _rightToolDock2.Height > RIGHT_BAR_MIN_H)
                            {
                                int canReduce = _rightToolDock2.Height - RIGHT_BAR_MIN_H;
                                int reduce = Math.Min(canReduce, need);
                                _rightToolDock2.Height -= reduce;
                                _rightToolDock3.Top = _rightToolDock2.Bottom + RIGHT_BAR_GAP;
                                desired = viewerBottom - _rightToolDock3.Top;
                            }
                            desired = Math.Max(RIGHT_BAR_MIN_H, desired);
                        }

                        // viewerBottom을 초과하지 않도록 제한 (음수/과도 값 방지)
                        int maxAllow = Math.Max(RIGHT_BAR_MIN_H, viewerBottom - _rightToolDock3.Top);
                        h3 = Math.Min(Math.Max(RIGHT_BAR_MIN_H, desired), maxAllow);
                    }

                    _rightToolDock3.Height = h3;
                }

                // 최종 배치 후, 칩 폭 보정
                AdjustLabelChipWidths();
            }

            // 왼쪽 레일
            if (_leftRail != null)
                _leftRail.Padding = new Padding(0, topPad - RIGHT_DOCK_T, 0, 2);

            _canvasLayer.Invalidate();
        }

        private void ToggleMaximizeRestore()
        {
            WindowState = (WindowState == FormWindowState.Maximized)
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        }

        // ---- Buttons/Actions
        private void OnAddClick(object sender, EventArgs e)
        {
            EnsureLabelWindow();

            _labelAnchorBtn = (_btnAdd.Parent != null) ? (Control)_btnAdd.Parent : (Control)_btnAdd;
            Point pScreen = _labelAnchorBtn.PointToScreen(new Point(0, 0));
            Rectangle wa = Screen.FromControl(this).WorkingArea;

            int x = pScreen.X - _labelWin.Width - 12; // 버튼 왼쪽에 12px 간격
            int y = pScreen.Y + (_labelAnchorBtn.Height / 2) - (_labelWin.Height / 2);

            if (x < wa.Left) x = wa.Left + 8;
            if (y < wa.Top) y = wa.Top + 8;
            if (y + _labelWin.Height > wa.Bottom) y = wa.Bottom - _labelWin.Height - 8;

            _labelWin.StartPosition = FormStartPosition.Manual;
            _labelWin.Location = new Point(x, y);

            var result = _labelWin.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                string name = _labelWin.LabelName;
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = "Label" + _labelSeq.ToString();
                    _labelSeq++;
                }
                Color col = _labelWin.SelectedColor;
                AddLabelChip(name, col);
            }

            if (_canvas != null && !_canvas.Focused) _canvas.Focus();
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            try
            {
                if (_canvas == null || _canvas.Image == null)
                {
                    MessageBox.Show(this, "이미지가 없습니다.", "SAVE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                SaveDatasetYoloWithImages();
                if (_canvas != null) _canvas.ClearSelectionAndResetEditing();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "저장 중 오류: " + ex.Message, "SAVE", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (_canvas != null && !_canvas.Focused) _canvas.Focus();
            }
        }
        private string PickAnnotationDataFolderWithCommonDialog(string initialDir = null)
        {
            using (var dlg = new CommonOpenFileDialog())
            {
                dlg.Title = "AnnotationData 폴더를 선택하세요";
                dlg.IsFolderPicker = true;
                dlg.Multiselect = false;
                dlg.EnsurePathExists = true;
                dlg.AllowNonFileSystemItems = false;
                if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
                    dlg.InitialDirectory = initialDir;

                var ret = dlg.ShowDialog();
                if (ret != CommonFileDialogResult.Ok) return null;

                var di = new DirectoryInfo(dlg.FileName);
                if (!di.Name.Equals("AnnotationData", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "선택한 폴더의 이름이 'AnnotationData'가 아닙니다.",
                                    "EXPORT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }
                return di.FullName; // AnnotationData 경로
            }
        }
        string pickedPath = "";
        private async void OnExportClick(object sender, EventArgs e)
        {
            try
            {
                pickedPath = PickAnnotationDataFolderWithCommonDialog(null); // 초기경로 없으면 null
                if (string.IsNullOrEmpty(pickedPath)) return;

                var modelDir = Path.Combine(pickedPath, "Model");
                var imagesDir = Path.Combine(modelDir, "images");
                var labelsDir = Path.Combine(modelDir, "labels");
                var classesPath = Path.Combine(modelDir, "classes.txt");
                var notesPath = Path.Combine(modelDir, "notes.json");

                if (!Directory.Exists(modelDir) || !Directory.Exists(imagesDir) || !Directory.Exists(labelsDir)
                    || !File.Exists(classesPath) || !File.Exists(notesPath))
                {
                    MessageBox.Show(this, "AnnotationData\\Model 폴더에 images, labels, classes.txt, notes.json이 있는지 확인하세요.",
                                    "EXPORT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using (var splitDlg = new ExportSplitDialog(90, 5, 5))
                {
                    if (splitDlg.ShowDialog(this) != DialogResult.OK) return;
                    int pTrain = splitDlg.TrainPercent;
                    int pVal = splitDlg.ValPercent;
                    int pTest = splitDlg.TestPercent;

                    if (pTrain + pVal + pTest != 100)
                    {
                        MessageBox.Show(this, "세 비율의 합이 100%가 아닙니다.", "EXPORT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var resultRoot = Path.Combine(pickedPath, "Result");

                    int total = 0, nTrain = 0, nVal = 0, nTest = 0;
                    string zipPath = null;

                    using (var overlay = new ProgressOverlay(this, "Model Exporting..."))
                    {
                        overlay.Report(0, "Preparing...");
                        await Task.Run(() =>
                        {
                            overlay.Report(5, "Exporting dataset...");
                            DoYoloSegExport(modelDir, resultRoot, pTrain, pVal, pTest, out total, out nTrain, out nVal, out nTest);
                            overlay.Report(40, "Packaging to ZIP...");
                            zipPath = CreateResultZip(resultRoot, null, (p, msg) => overlay.Report(40 + (int)((p * 60L) / 100), msg) // 40%→100% 구간에 매핑
                            );
                        });

                        overlay.Report(100, "완료");
                    }

                    using (var dlg = new ExportResultDialog(total, nTrain, nVal, nTest, resultRoot, zipPath))
                    {
                        dlg.ShowDialog(this);
                    }

                    //try
                    //{
                    //    if (Directory.Exists(resultRoot))
                    //    {
                    //        Process.Start("explorer.exe", "\"" + resultRoot + "\"");
                    //    }
                    //}
                    //catch { /* 탐색기 열기 실패는 무시 */ }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "내보내기 중 오류: " + ex.Message, "EXPORT",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (this.ActiveControl != null && !(this.ActiveControl is Form)) this.ActiveControl.Focus();
            }
        }


        private string CreateResultZip(string resultRoot, string zipFileName = null, Action<int, string> onProgress = null)
        {
            if (string.IsNullOrWhiteSpace(resultRoot) || !Directory.Exists(resultRoot))
                throw new DirectoryNotFoundException("Result 폴더가 없습니다: " + resultRoot);

            var dirInfo = new DirectoryInfo(resultRoot);
            var finalZip = Path.Combine(resultRoot, zipFileName ?? (dirInfo.Name + ".zip"));

            // 임시 위치에 만들고 나중에 이동(자기 자신 포함 방지)
            var tempZip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".zip");

            // 상대 경로 계산( .NET Framework 호환용 )
            Func<string, string, string> relPath = (root, path) =>
            {
                var r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var p = Path.GetFullPath(path);
                if (!p.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return Path.GetFileName(path);
                var rel = p.Substring(r.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return rel.Replace('\\', '/'); // ZIP 표준 경로 구분자
            };

            // 대상으로 할 파일 목록(최종 zip 자신은 제외)
            var allFiles = Directory
                .GetFiles(resultRoot, "*", SearchOption.AllDirectories)
                .Where(f => !string.Equals(f, finalZip, StringComparison.OrdinalIgnoreCase))
                .ToList();

            int total = allFiles.Count;
            int done = 0;

            using (var zip = ZipFile.Open(tempZip, ZipArchiveMode.Create))
            {
                foreach (var file in allFiles)
                {
                    var entryName = relPath(resultRoot, file);
                    zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);

                    done++;
                    if (onProgress != null)
                    {
                        int percent = total == 0 ? 100 : (int)((done * 100L) / total);
                        onProgress(percent, Path.GetFileName(file)); // (진행률, 현재 파일명)
                    }
                }
            }

            // 기존 ZIP 있으면 대체
            if (File.Exists(finalZip)) File.Delete(finalZip);
            File.Move(tempZip, finalZip);

            return finalZip;
        }


        /// <summary>
        /// AnnotationData\\Model을 읽어 YOLO Segmentation 데이터셋으로 재구성하여 RESULTMODEL에 저장
        /// </summary>
        private void DoYoloSegExport(string modelDir, string resultRoot, int pctTrain, int pctVal, int pctTest,
                                     out int total, out int nTrain, out int nVal, out int nTest)
        {
            var imagesDir = Path.Combine(modelDir, "images");
            var labelsDir = Path.Combine(modelDir, "labels");
            var classesPath = Path.Combine(modelDir, "classes.txt");
            var notesPath = Path.Combine(modelDir, "notes.json");

            if (!Directory.Exists(imagesDir) || !Directory.Exists(labelsDir) || !File.Exists(classesPath))
                throw new InvalidOperationException("Model 폴더 구조가 올바르지 않습니다.");

            // 1) classes 로드
            var classNames = File.ReadAllLines(classesPath, Encoding.UTF8)
                                 .Select(s => (s ?? "").Trim())
                                 .Where(s => s.Length > 0)
                                 .ToList();
            if (classNames.Count == 0) classNames.Add("Default");

            // 2) 이미지/라벨 페어 매칭 (대소문자 무시)
            var images = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var imgPath in Directory.EnumerateFiles(imagesDir, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(imgPath);
                if (string.IsNullOrEmpty(ext) || !_imgExts.Contains(ext)) continue;
                images[Path.GetFileNameWithoutExtension(imgPath).ToLowerInvariant()] = imgPath;
            }
            foreach (var labPath in Directory.EnumerateFiles(labelsDir, "*.txt", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(labPath);
                if (name.Equals("classes.txt", StringComparison.OrdinalIgnoreCase)) continue;
                labels[Path.GetFileNameWithoutExtension(labPath).ToLowerInvariant()] = labPath;
            }

            var pairs = new List<Tuple<string, string>>();
            foreach (var kv in images)
            {
                string lab;
                if (labels.TryGetValue(kv.Key, out lab))
                    pairs.Add(Tuple.Create(kv.Value, lab));
            }
            if (pairs.Count == 0) throw new InvalidOperationException("이미지-라벨 쌍을 찾지 못했습니다.");

            // 3) 셔플 & 분할
            Shuffle(pairs, 0);
            total = pairs.Count;
            nVal = (int)Math.Round(total * (pctVal / 100.0));
            nTest = (int)Math.Round(total * (pctTest / 100.0));
            nTrain = total - nVal - nTest;
            if (nTrain < 0) nTrain = 0;

            var trainSet = pairs.Take(nTrain).ToList();
            var valSet = pairs.Skip(nTrain).Take(nVal).ToList();
            var testSet = pairs.Skip(nTrain + nVal).Take(nTest).ToList();

            // 4) 결과 폴더 구조
            var subDirs = new[]
            {
                Path.Combine(resultRoot, "images", "train"),
                Path.Combine(resultRoot, "images", "val"),
                Path.Combine(resultRoot, "images", "test"),
                Path.Combine(resultRoot, "labels", "train"),
                Path.Combine(resultRoot, "labels", "val"),
                Path.Combine(resultRoot, "labels", "test"),
            };
            foreach (var d in subDirs) Directory.CreateDirectory(d);

            // 5) 복사
            CopyPairs(trainSet, Path.Combine(resultRoot, "images", "train"), Path.Combine(resultRoot, "labels", "train"));
            CopyPairs(valSet, Path.Combine(resultRoot, "images", "val"), Path.Combine(resultRoot, "labels", "val"));
            CopyPairs(testSet, Path.Combine(resultRoot, "images", "test"), Path.Combine(resultRoot, "labels", "test"));

            // 6) data.yaml 생성
            var sb = new StringBuilder();
            sb.AppendLine("path: " + QuoteYamlPath(resultRoot));
            sb.AppendLine("train: images/train");
            sb.AppendLine("val: images/val");
            sb.AppendLine("test: images/test");
            sb.AppendLine("names:");
            for (int i = 0; i < classNames.Count; i++)
                sb.AppendLine($"  {i}: {EscapeYaml(classNames[i])}");
            sb.AppendLine("task: seg");
            File.WriteAllText(Path.Combine(resultRoot, "data.yaml"), sb.ToString(), Encoding.UTF8);
        }

        private static string QuoteYamlPath(string p)
        {
            if (string.IsNullOrEmpty(p)) return "''";
            // 공백/특수문자 포함 시 작은따옴표
            if (p.IndexOfAny(new[] { ' ', ':', '#', '{', '}', '[', ']', ',', '&', '*', '?', '|', '<', '>', '=', '!', '%', '@', '\\' }) >= 0)
                return "'" + p.Replace("'", "''") + "'";
            return p;
        }

        private static string EscapeYaml(string s)
        {
            if (s == null) return "''";
            // 일반적으로 이름엔 따옴표만 이스케이프
            if (s.IndexOfAny(new[] { ':', '#', '-', '?', '{', '}', ',', '&', '*', '!', '|', '>', '\'', '\"', '%', '@', '`' }) >= 0 || s.Contains(" "))
                return "'" + s.Replace("'", "''") + "'";
            return s;
        }

        private static void CopyPairs(List<Tuple<string, string>> pairs, string imgDstDir, string labDstDir)
        {
            foreach (var t in pairs)
            {
                var img = t.Item1; var lab = t.Item2;
                var imgName = Path.GetFileName(img);
                var labName = Path.GetFileName(lab);
                File.Copy(img, Path.Combine(imgDstDir, imgName), true);
                File.Copy(lab, Path.Combine(labDstDir, labName), true);
            }
        }

        private static void Shuffle<T>(IList<T> list, int seed)
        {
            var rnd = new Random(seed);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                OnSaveClick(_btnSave, EventArgs.Empty); // 또는 내부 저장 호출
                return true;
            }
            if (keyData == (Keys.Control | Keys.Up) || keyData == (Keys.Control | Keys.Down))
            {
                if (_canvas != null && !_canvas.HasSelection)
                {
                    if ((keyData & Keys.KeyCode) == Keys.Up)
                        NavigateToPreviousImage();
                    else
                        NavigateToNextImage();
                    return true;
                }
                // 선택이 있을 때는 캔버스가 화살표 입력(도형 이동 등)을 처리
            }


            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void EnsureLabelWindow()
        {
            if (_labelWin == null || _labelWin.IsDisposed)
            {
                _labelWin = new LabelCreateWindow();
            }
            _labelWin.ResetForNewLabel(); // 이름 빈칸 초기화, 미리보기 동기화
        }

        // ---------- 라벨 칩 ----------
        private void AddLabelChip(string labelName, Color color)
        {
            if (_rightTools2 == null) return;

            int chipW = Math.Max(LABEL_CHIP_MIN_W, _btnAdd.Width); // ★ ADD와 동일 폭
            int chipH = 24;

            var chip = MakeLabelChip(labelName, color, chipW, chipH);

            _rightTools2.Controls.Add(chip);
            SelectOnly(chip);

            chip.MouseDown += (s, e) => { if (!_canvas.Focused) _canvas.Focus(); };
            chip.Click += (s, e) => { if (!_canvas.Focused) _canvas.Focus(); };

            AdjustLabelChipWidths();
        }

        private Guna2Panel MakeLabelChip(string labelName, Color color, int width, int height)
        {
            var chip = new Guna2Panel();
            chip.Tag = new LabelInfo(labelName, color);
            chip.FillColor = Color.White;
            chip.BorderColor = Color.Silver;
            chip.BorderThickness = 2;
            chip.BorderRadius = 10;
            chip.Size = new Size(Math.Max(120, width), height);
            chip.Margin = new Padding(0, 0, 0, 8);
            chip.Cursor = Cursors.Hand;

            chip.ShadowDecoration.Enabled = false;
            chip.ShadowDecoration.Color = Color.LimeGreen;
            chip.ShadowDecoration.Shadow = new Padding(4);
            chip.ShadowDecoration.BorderRadius = chip.BorderRadius;
            chip.ShadowDecoration.Parent = chip;

            var swatch = new Guna2Panel();
            swatch.Name = "__LabelSwatch";
            swatch.Size = new Size(18, 18);
            swatch.BorderRadius = 4;
            swatch.FillColor = color;
            swatch.BorderColor = Color.Silver;
            swatch.BorderThickness = 1;
            swatch.Location = new Point(2, (chip.Height - swatch.Height) / 2);

            var nameLbl = new Guna2HtmlLabel();
            nameLbl.Name = "__LabelText";
            nameLbl.BackColor = Color.Transparent;
            nameLbl.AutoSize = true;
            nameLbl.Text = labelName;
            nameLbl.Location = new Point(swatch.Right + 2, (chip.Height - nameLbl.Height) / 2);

            chip.Click += OnChipClick_Simple;
            swatch.Click += OnChipClick_Simple;
            nameLbl.Click += OnChipClick_Simple;

            chip.Controls.Add(swatch);
            chip.Controls.Add(nameLbl);

            chip.Resize += (s, e) =>
            {
                swatch.Location = new Point(10, (chip.Height - swatch.Height) / 2);
                nameLbl.Location = new Point(swatch.Right + 2, (chip.Height - nameLbl.Height) / 2);
            };

            return chip;
        }

        private void OnChipClick_Simple(object sender, System.EventArgs e)
        {
            var chip = FindChipFrom(sender as Control);
            if (chip != null)
                SelectOnly(chip);
            if (_canvas != null && !_canvas.Focused) _canvas.Focus();
        }

        private void UpdateActiveLabelFromChip(Guna2Panel chip)
        {
            if (chip == null || _canvas == null) return;

            string name = null;
            Color stroke = Color.DeepSkyBlue;

            if (chip.Tag is LabelInfo)
            {
                var li = (LabelInfo)chip.Tag;
                name = li.Name;
                stroke = li.Color;
            }
            else
            {
                var swatch = chip.Controls.OfType<Guna2Panel>().FirstOrDefault();
                var nameLbl = chip.Controls.OfType<Guna2HtmlLabel>().FirstOrDefault();
                if (nameLbl != null) name = nameLbl.Text;
                if (swatch != null) stroke = swatch.FillColor;
            }

            var fill = Color.FromArgb(72, stroke);
            _canvas.SetActiveLabel(name ?? "Default", stroke, fill);
        }

        private Guna2Panel FindChipFrom(Control c)
        {
            while (c != null)
            {
                var gp = c as Guna2Panel;
                if (gp != null)
                {
                    if (gp.Tag is LabelInfo || object.Equals(gp.Tag, "LabelChip"))
                        return gp;
                }
                c = c.Parent;
            }
            return null;
        }

        private void SelectOnly(Guna2Panel target)
        {
            if (_rightTools2 == null) return;

            for (int i = 0; i < _rightTools2.Controls.Count; i++)
            {
                var chip = _rightTools2.Controls[i] as Guna2Panel;
                if (chip != null && (chip.Tag is LabelInfo || object.Equals(chip.Tag, "LabelChip")))
                {
                    bool selected = (chip == target);
                    SetSelected(chip, selected);
                    if (selected) UpdateActiveLabelFromChip(chip);
                }
            }
        }

        private void AddDefaultLabelIfMissing()
        {
            bool hasAny = _rightTools2.Controls.OfType<Guna2Panel>()
                           .Any(p => p.Tag is LabelInfo);
            if (hasAny) return;

            AddLabelChip("Default", Color.DeepSkyBlue);
        }

        private void SetSelected(Guna2Panel chip, bool selected)
        {
            chip.ShadowDecoration.Enabled = selected;
            chip.BorderColor = selected ? Color.FromArgb(60, 180, 75) : Color.Silver;
            chip.BorderThickness = 2;
        }

        private void AdjustLabelChipWidths()
        {
            if (_rightToolDock2 == null || _rightTools2 == null || _btnAdd == null) return;
            int targetW = Math.Max(LABEL_CHIP_MIN_W, _btnAdd.Width);

            for (int i = 0; i < _rightTools2.Controls.Count; i++)
            {
                var chip = _rightTools2.Controls[i] as Guna2Panel;
                if (chip != null && (chip.Tag is LabelInfo || object.Equals(chip.Tag, "LabelChip")))
                {
                    chip.Width = targetW;
                }
            }
        }

        // ------------------------------------------------------------

        private void OnOpenClick(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "이미지 파일 또는 폴더 열기";
                dlg.Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|모든 파일|*.*";
                dlg.Multiselect = false;

                dlg.CheckFileExists = false;
                dlg.ValidateNames = false;
                dlg.FileName = "폴더를 선택하려면 이 항목을 클릭하세요";

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var chosen = dlg.FileName;

                if (System.IO.File.Exists(chosen))
                {
                    if (IsImageFile(chosen))
                    {
                        try
                        #region 8) Utilities & Helpers (기타 보조 함수)
                        #endregion  // 6) Event Handlers
                        {
                            LoadImageAtPath(chosen);
                            var folder = System.IO.Path.GetDirectoryName(chosen);
                            if (!string.IsNullOrEmpty(folder) && System.IO.Directory.Exists(folder))
                            {
                                PopulateTreeFromFolder(folder);
                                SelectNodeByPath(chosen);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, "이미지 로드 오류: " + ex.Message, "오류",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        var folder = System.IO.Path.GetDirectoryName(chosen);
                        if (!string.IsNullOrEmpty(folder) && System.IO.Directory.Exists(folder))
                        {
                            PopulateTreeFromFolder(folder);
                        }
                    }
                    return;
                }

                var maybeFolder = System.IO.Path.GetDirectoryName(chosen);
                if (!string.IsNullOrEmpty(maybeFolder) && System.IO.Directory.Exists(maybeFolder))
                {
                    PopulateTreeFromFolder(maybeFolder);
                    return;
                }

                if (System.IO.Directory.Exists(chosen))
                {
                    PopulateTreeFromFolder(chosen);
                    return;
                }
            }
        }

        private bool TryLoadYoloForCurrentImage()
        {
            try
            {
                if (_canvas == null || _canvas.Image == null || string.IsNullOrEmpty(_currentImagePath)) return false;

                var root = FindDatasetRootForImage(_currentImagePath);
                if (root == null) return false;

                var classesPath = Path.Combine(root, "classes.txt");
                var labelsPath = Path.Combine(root, "labels", Path.GetFileNameWithoutExtension(_currentImagePath) + ".txt");
                if (!File.Exists(classesPath) || !File.Exists(labelsPath)) return false;

                // 1) classes 먼저 파싱
                var classes = ParseClassesTxt(classesPath);
                if (classes == null || classes.Count == 0) classes = new List<string> { "Default" };

                // 2) notes.json에 저장된 색상 있으면 먼저 불러오기 → 칩/맵 동기화
                LoadClassColorsFromNotesJson(root, classes);
                RebuildClassColorMapFromChips();

                // 3) 라벨 파일 로드 (색상은 _classColorMap을 통해 적용됨)
                LoadYoloLabelFile(labelsPath, classes);

                return _canvas.Shapes != null && _canvas.Shapes.Count > 0;
            }
            catch { return false; }
        }

        private void RebuildClassColorMapFromChips()
        {
            _classColorMap.Clear();
            if (_rightTools2 == null) return;

            foreach (Control c in _rightTools2.Controls)
            {
                var pnl = c as Guna2Panel;
                if (pnl == null || !(pnl.Tag is LabelInfo)) continue;

                var li = (LabelInfo)pnl.Tag;

                // 이름
                string name = null;
                try
                {
                    var nprop = li.GetType().GetProperty("Name");
                    if (nprop != null) name = nprop.GetValue(li, null) as string;
                }
                catch { /* ignore */ }
                if (string.IsNullOrWhiteSpace(name))
                    name = pnl.Text;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                // 색상
                Color baseColor = Color.Empty;
                try
                {
                    var pColor = li.GetType().GetProperty("Color");
                    if (pColor != null)
                    {
                        var v = pColor.GetValue(li, null);
                        if (v is Color col) baseColor = col;
                    }
                    if (baseColor.IsEmpty)
                    {
                        var pFill = li.GetType().GetProperty("FillColor");
                        if (pFill != null)
                        {
                            var v = pFill.GetValue(li, null);
                            if (v is Color col2) baseColor = col2;
                        }
                    }
                    if (baseColor.IsEmpty)
                    {
                        var pStroke = li.GetType().GetProperty("StrokeColor");
                        if (pStroke != null)
                        {
                            var v = pStroke.GetValue(li, null);
                            if (v is Color col3) baseColor = col3;
                        }
                    }
                }
                catch { /* ignore */ }

                if (baseColor.IsEmpty)
                    baseColor = ColorFromNameDeterministic(name); // 이름으로 고정색 생성

                if (!_classColorMap.ContainsKey(name))
                    _classColorMap.Add(name, baseColor);
            }
        }

        // 이름으로 항상 같은 색을 만드는 간단한 함수(안 겹치게 은근 다양함)
        private Color ColorFromNameDeterministic(string name)
        {
            unchecked
            {
                int h = 23;
                for (int i = 0; i < name.Length; i++)
                    h = h * 31 + name[i];

                // 0..63 범위를 3채널로 쪼개 사용 (128~240로 제한)
                int r = 128 + ((h) & 63) * 2;
                int g = 128 + ((h >> 6) & 63) * 2;
                int b = 128 + ((h >> 12) & 63) * 2;

                r = Math.Max(32, Math.Min(240, r));
                g = Math.Max(32, Math.Min(240, g));
                b = Math.Max(32, Math.Min(240, b));
                return Color.FromArgb(r, g, b);
            }
        }

        // stroke/fill 색 꺼내기(없으면 이름 고정색 + 반투명 fill)
        private void GetColorsForClass(string labelName, out Color stroke, out Color fill)
        {
            if (string.IsNullOrWhiteSpace(labelName)) labelName = "Default";

            Color baseColor;
            if (!_classColorMap.TryGetValue(labelName, out baseColor))
                baseColor = ColorFromNameDeterministic(labelName);

            stroke = baseColor;
            fill = Color.FromArgb(72, baseColor); // 기존 스타일과 맞춤(반투명)
        }


        // 이미지 경로로부터 데이터셋 루트 추정:  .../images/xxx.ext  → 루트는 images 상위
        private string FindDatasetRootForImage(string imagePath)
        {
            var dir = new DirectoryInfo(Path.GetDirectoryName(imagePath));
            var baseName = Path.GetFileNameWithoutExtension(imagePath);

            if (!string.IsNullOrEmpty(_lastYoloExportRoot))
            {
                var cp = Path.Combine(_lastYoloExportRoot, "classes.txt");
                var lp = Path.Combine(_lastYoloExportRoot, "labels", baseName + ".txt");
                if (File.Exists(cp) && File.Exists(lp))
                    return _lastYoloExportRoot;  // ★ 원본 이미지가 다른 폴더여도 이 루트를 사용
            }
            // 케이스 A: 실제로 images 폴더 아래에서 열었을 때
            if (dir != null && dir.Name.Equals("images", StringComparison.OrdinalIgnoreCase) && dir.Parent != null)
            {
                var root = dir.Parent.FullName;
                var classesOk = File.Exists(Path.Combine(root, "classes.txt"));
                var labelOk = File.Exists(Path.Combine(root, "labels", baseName + ".txt"));
                if (classesOk && labelOk) return root;
            }

            // 케이스 B: 같은 폴더이거나 임의 폴더에서 열었을 때 → 근처에서 루트 추적
            // 상위 3단계까지 올라가며 classes.txt + labels/<파일>.txt를 찾음
            var walk = dir;
            for (int i = 0; i < 3 && walk != null; i++, walk = walk.Parent)
            {
                var candidate = walk.FullName;
                if (File.Exists(Path.Combine(candidate, "classes.txt")) &&
                    File.Exists(Path.Combine(candidate, "labels", baseName + ".txt")))
                    return candidate;
            }
            return null;
        }

        private List<string> ParseClassesTxt(string classesPath)
        {
            var list = new List<string>();
            foreach (var raw in File.ReadAllLines(classesPath, Encoding.UTF8))
            {
                var s = (raw ?? "").Trim();
                if (s.Length > 0) list.Add(s);
            }
            if (list.Count == 0) list.Add("Default");
            return list;
        }

        private void LoadYoloLabelFile(string labelPath, List<string> classes)
        {
            var img = _canvas.Image; int W = img.Width, H = img.Height;
            var ci = CultureInfo.InvariantCulture;

            _canvas.ClearAllShapes();

            foreach (var raw in File.ReadAllLines(labelPath))
            {
                var line = (raw ?? "").Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                var tok = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (tok.Length < 5) continue;

                int cls;
                if (!int.TryParse(tok[0], out cls)) continue;
                string labelName = (cls >= 0 && cls < classes.Count) ? classes[cls] : "cls_" + cls.ToString();

                // 박스: 5 토큰 (cls cx cy w h)
                if (tok.Length == 5)
                {
                    float cx = float.Parse(tok[1], ci) * W;
                    float cy = float.Parse(tok[2], ci) * H;
                    float ww = float.Parse(tok[3], ci) * W;
                    float hh = float.Parse(tok[4], ci) * H;

                    var r = new RectangleF(cx - ww * 0.5f, cy - hh * 0.5f, ww, hh);

                    Color stroke, fill;
                    GetColorsForClass(labelName, out stroke, out fill);
                    _canvas.AddBox(r, labelName, stroke, fill);
                    continue;
                }

                // 폴리곤: (cls x1 y1 x2 y2 ...), 좌표쌍 개수 검사
                if (((tok.Length - 1) % 2) == 0 && (tok.Length - 1) >= 6)
                {
                    var pts = new List<PointF>();
                    for (int i = 1; i < tok.Length; i += 2)
                    {
                        float nx = float.Parse(tok[i], ci);
                        float ny = float.Parse(tok[i + 1], ci);
                        pts.Add(new PointF(nx * W, ny * H));
                    }

                    Color stroke2, fill2;
                    GetColorsForClass(labelName, out stroke2, out fill2);
                    _canvas.AddPolygon(pts, labelName, stroke2, fill2);
                }
            }
        }

        // ---- 파일/트리
        private static bool IsImageFile(string path)
        {
            try
            {
                var ext = Path.GetExtension(path);
                return !string.IsNullOrEmpty(ext) && _imgExts.Contains(ext);
            }
            catch { return false; }
        }
        private void PopulateTreeFromFolder(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) return;

            _currentFolder = rootPath;
            TryBindAnnotationRootNear(rootPath);
            _fileTree.BeginUpdate();
            _fileTree.Nodes.Clear();

            var root = CreateFolderNode(rootPath);
            root.Text = new DirectoryInfo(rootPath).Name;
            _fileTree.Nodes.Add(root);
            root.Expand();

            _fileTree.EndUpdate();
        }
        private TreeNode CreateFolderNode(string folder)
        {
            var node = new TreeNode(Path.GetFileName(folder)) { Tag = folder };

            try
            {
                foreach (var sub in Directory.GetDirectories(folder))
                {
                    var subNode = CreateFolderNode(sub);
                    node.Nodes.Add(subNode);
                }
            }
            catch { /* 권한 문제 등 무시 */ }

            try
            {
                var files = Directory.GetFiles(folder)
                                     .Where(IsImageFile)
                                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                     .ToArray();
                foreach (var f in files)
                {
                    var fn = Path.GetFileName(f);
                    var imgNode = new TreeNode(fn) { Tag = f };
                    LabelStatusService.ApplyNodeState(imgNode, f, _lastYoloExportRoot, showCountSuffix: true);
                    node.Nodes.Add(imgNode);
                }
            }
            catch { /* 무시 */ }

            return node;
        }
        private void SelectNodeByPath(string fullPath)
        {
            if (_fileTree.Nodes.Count == 0) return;

            TreeNode found = null;
            var q = new Queue<TreeNode>();
            foreach (TreeNode n in _fileTree.Nodes) q.Enqueue(n);

            while (q.Count > 0)
            {
                var n = q.Dequeue();
                var tag = n.Tag as string;
                if (string.Equals(tag, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    found = n; break;
                }
                foreach (TreeNode c in n.Nodes) q.Enqueue(c);
            }

            if (found != null)
            {
                _fileTree.SelectedNode = found;
                found.EnsureVisible();
            }
        }
        private void LoadImageAtPath(string path)
        {
            try
            {
                using (var temp = Image.FromFile(path))
                {
                    var bmp = new Bitmap(temp);
                    _canvas.LoadImage(bmp);
                    _canvas.ZoomToFit();
                    _currentImagePath = path;
                    if (!_canvas.Focused) _canvas.Focus();
                    _canvasLayer.Invalidate();
                    _yoloLoadedForCurrentImage = false;
                    TryBindAnnotationRootNear(Path.GetDirectoryName(_currentImagePath));
                    _yoloLoadedForCurrentImage = TryLoadYoloForCurrentImage();
                }

                BeginInvoke(new Action(() =>
                {
                    if (_canvas != null && _canvas.CanFocus) _canvas.Focus();    // ★ 안전망
                }));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "이미지 로드 오류: " + ex.Message, "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ===== 이미지 네비게이션 (Ctrl+Up/Down) =====
        private bool IsImageNode(TreeNode n)
        {
            if (n == null) return false;
            var path = n.Tag as string;
            return !string.IsNullOrEmpty(path) && File.Exists(path) && IsImageFile(path);
        }

        private TreeNode FindCurrentImageNode()
        {
            var sel = (_fileTree != null) ? _fileTree.SelectedNode : null;
            if (IsImageNode(sel)) return sel;

            if (!string.IsNullOrEmpty(_currentImagePath))
            {
                var byPath = FindNodeByImagePath(_currentImagePath);
                if (IsImageNode(byPath)) return byPath;
            }
            return null;
        }

        private List<TreeNode> GetAllImageNodes()
        {
            var list = new List<TreeNode>();
            if (_fileTree == null || _fileTree.Nodes.Count == 0) return list;

            var stack = new Stack<TreeNode>();
            foreach (TreeNode n in _fileTree.Nodes) stack.Push(n);

            // DFS (루트부터 아래로, TreeView 표시 순서 유지 위해 역순 push)
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (IsImageNode(node)) list.Add(node);

                // push children in reverse so that left-to-right traversal is preserved
                for (int i = node.Nodes.Count - 1; i >= 0; i--)
                    stack.Push(node.Nodes[i]);
            }
            return list;
        }

        private void OpenImageFromNode(TreeNode node)
        {
            if (node == null) return;
            var path = node.Tag as string;
            if (string.IsNullOrEmpty(path)) return;
            // TreeView 선택만 바꿔도 AfterSelect에서 LoadImageAtPath가 호출됨
            if (_fileTree != null) _fileTree.SelectedNode = node;
            else LoadImageAtPath(path);
        }

        private void NavigateToNextImage()
        {
            var nodes = GetAllImageNodes();
            if (nodes.Count == 0) return;

            var cur = FindCurrentImageNode();
            int idx = (cur != null) ? nodes.IndexOf(cur) : -1;
            if (idx < 0) idx = -1; // not found or none selected

            if (idx + 1 < nodes.Count)
                OpenImageFromNode(nodes[idx + 1]);
        }

        private void NavigateToPreviousImage()
        {
            var nodes = GetAllImageNodes();
            if (nodes.Count == 0) return;

            var cur = FindCurrentImageNode();
            int idx = (cur != null) ? nodes.IndexOf(cur) : -1;

            if (idx > 0)
                OpenImageFromNode(nodes[idx - 1]);
        }


        #endregion  // 8) Utilities & Helpers

        #region 6) Export / Import (YOLO Segmentation)

        private List<string> GetCurrentClasses()
        {
            var classes = new List<string>();
            if (_rightTools2 != null)
            {
                for (int i = 0; i < _rightTools2.Controls.Count; i++)
                {
                    var chip = _rightTools2.Controls[i] as Guna2Panel;
                    if (chip != null && chip.Tag is LabelInfo)
                    {
                        var li = (LabelInfo)chip.Tag;
                        if (!string.IsNullOrWhiteSpace(li.Name) && !classes.Contains(li.Name))
                            classes.Add(li.Name);
                    }
                }
            }
            if (classes.Count == 0) classes.Add("Default");
            return classes;
        }
        private void SaveDatasetYoloWithImages()
        {
            if (_canvas == null || _canvas.Image == null)
                throw new InvalidOperationException("이미지가 없습니다.");

            if (string.IsNullOrEmpty(_currentImagePath) || !File.Exists(_currentImagePath))
                throw new InvalidOperationException("현재 이미지 경로를 찾을 수 없습니다.");

            // 0) 저장 루트 결정
            var baseDir = Path.GetDirectoryName(_currentImagePath);
            var annotationRoot = Path.Combine(baseDir, "AnnotationData");
            var rootDir = Path.Combine(annotationRoot, "Model");

            // ✅ LabelStatusService 저장소 루트 지정 (배지/DB를 이 위치에 유지)
            LabelStatusService.SetStorageRoot(annotationRoot);

            // 1) 폴더 구조
            var imagesDir = Path.Combine(rootDir, "images");
            var labelsDir = Path.Combine(rootDir, "labels");
            Directory.CreateDirectory(imagesDir);
            Directory.CreateDirectory(labelsDir);

            // 2) classes.txt / notes.json
            var classes = GetCurrentClasses(); // 기존 코드 그대로 사용
            File.WriteAllLines(Path.Combine(rootDir, "classes.txt"), classes, Encoding.UTF8);
            SaveNotesJson(Path.Combine(rootDir, "notes.json"), classes);
            _lastYoloExportRoot = rootDir; // ✅ 이후 배지 적용에 필요

            // 3) 이미지 복사
            var srcExt = Path.GetExtension(_currentImagePath);
            var baseName = Path.GetFileNameWithoutExtension(_currentImagePath);
            var dstImagePath = Path.Combine(imagesDir, baseName + srcExt);
            File.Copy(_currentImagePath, dstImagePath, true);

            // 4) YOLO 라벨 작성
            var dstLabelPath = Path.Combine(labelsDir, baseName + ".txt");
            WriteYoloLabelForCurrentImage(dstLabelPath, classes);

            // 5) ✅ DB에 라벨 상태 반영 (원본 이미지 체크/개수 업데이트용)
            LabelStatusService.MarkLabeled(_currentImagePath, _canvas.Shapes.Count);

            // 6) ✅ 트리뷰 해당 노드에 배지 재적용 (초록 체크 + 개수 툴팁)
            try
            {
                var node = FindNodeByImagePath(_currentImagePath);
                if (node != null)
                    LabelStatusService.ApplyNodeState(node, _currentImagePath, _lastYoloExportRoot, showCountSuffix: true);
            }
            catch { /* 배지 리프레시 실패는 무시 */ }
        }


        private TreeNode FindNodeByImagePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return null;
            return FindNodeByImagePathRec(_fileTree.Nodes, fullPath);
        }
        private TreeNode FindNodeByImagePathRec(TreeNodeCollection nodes, string fullPath)
        {
            foreach (TreeNode n in nodes)
            {
                var tagPath = n.Tag as string;
                if (!string.IsNullOrEmpty(tagPath) &&
                    tagPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                    return n;

                if (n.Nodes.Count > 0)
                {
                    var hit = FindNodeByImagePathRec(n.Nodes, fullPath);
                    if (hit != null) return hit;
                }
            }
            return null;
        }


        private void SaveNotesJson(string path, List<string> classes)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            // categories (for compatibility)
            sb.AppendLine("  \"categories\": [");
            for (int i = 0; i < classes.Count; i++)
            {
                sb.Append("    { \"id\": ").Append(i)
                  .Append(", \"name\": \"").Append(EscapeJson(classes[i])).Append("\" }");
                if (i < classes.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ],");

            // colors map
            sb.AppendLine("  \"colors\": {");
            for (int i = 0; i < classes.Count; i++)
            {
                var name = classes[i];
                var c = GetBaseColorForClass(name);
                sb.Append("    \"").Append(EscapeJson(name)).Append("\": \"").Append(ToHexRgb(c)).Append("\"");
                if (i < classes.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  },");

            sb.AppendLine("  \"info\": {");
            sb.Append("    \"year\": ").Append(DateTime.Now.Year).AppendLine(",");
            sb.AppendLine("    \"version\": \"1.0\",");
            sb.AppendLine("    \"contributor\": \"SmartLabelingApp\"");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>Return the base stroke color for a class name using current chips/map, or a deterministic fallback.</summary>
        private Color GetBaseColorForClass(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "Default";
            // 1) from explicit map
            if (_classColorMap.TryGetValue(name, out var c) && c != Color.Empty) return c;

            // 2) from an existing chip
            if (_rightTools2 != null)
            {
                foreach (Control cc in _rightTools2.Controls)
                {
                    var pnl = cc as Guna2Panel;
                    if (pnl == null || !(pnl.Tag is LabelInfo)) continue;
                    var li = (LabelInfo)pnl.Tag;
                    if (string.Equals(li.Name, name, StringComparison.OrdinalIgnoreCase))
                        return li.Color;
                }
            }
            // 3) deterministic fallback
            return ColorFromNameDeterministic(name);
        }

        private static string ToHexRgb(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private static bool TryParseHexColor(string s, out Color color)
        {
            color = Color.Empty;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (s.StartsWith("#")) s = s.Substring(1);
            if (s.Length == 6)
            {
                try
                {
                    int r = int.Parse(s.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                    int g = int.Parse(s.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    int b = int.Parse(s.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    color = Color.FromArgb(r, g, b);
                    return true;
                }
                catch { return false; }
            }
            return false;
        }

        /// <summary>Apply a color to a chip (UI + Tag) creating the swatch if needed.</summary>
        private void ApplyColorToChip(Guna2Panel chip, Color color)
        {
            if (chip == null) return;
            // Update tag
            if (chip.Tag is LabelInfo li)
                chip.Tag = new LabelInfo(li.Name, color);
            // Update swatch (child panel)
            var swatch = chip.Controls.OfType<Guna2Panel>().FirstOrDefault(p => p.Name == "__LabelSwatch");
            if (swatch != null) swatch.FillColor = color;
        }

        /// <summary>Find chip by label name (case-insensitive).</summary>
        private Guna2Panel FindChipByName(string name)
        {
            if (_rightTools2 == null || string.IsNullOrWhiteSpace(name)) return null;
            foreach (Control cc in _rightTools2.Controls)
            {
                var pnl = cc as Guna2Panel;
                if (pnl == null || !(pnl.Tag is LabelInfo)) continue;
                var li = (LabelInfo)pnl.Tag;
                if (string.Equals(li.Name, name, StringComparison.OrdinalIgnoreCase))
                    return pnl;
            }
            return null;
        }

        /// <summary>
        /// Load preferred class colors from notes.json if it contains a "colors" object.
        /// Also syncs the right label chips to use those colors (creating chips if missing).
        /// </summary>
        private void LoadClassColorsFromNotesJson(string rootDir, List<string> classes)
        {
            try
            {
                var notesPath = Path.Combine(rootDir, "notes.json");
                if (!File.Exists(notesPath)) return;

                string json = File.ReadAllText(notesPath, Encoding.UTF8);
                // naive scan for "colors": { "name":"#RRGGBB" ... }
                var colors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

                // Very small, simple parser: look for "colors" object then key/value pairs
                int idx = json.IndexOf("colors", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    int brace = json.IndexOf('{', idx);
                    if (brace >= 0)
                    {
                        int depth = 0;
                        int end = -1;
                        for (int i = brace; i < json.Length; i++)
                        {
                            if (json[i] == '{') depth++;
                            else if (json[i] == '}')
                            {
                                depth--;
                                if (depth == 0) { end = i; break; }
                            }
                        }
                        if (end > brace)
                        {
                            string obj = json.Substring(brace + 1, end - brace - 1);
                            // split by commas at top level
                            var parts = obj.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var part in parts)
                            {
                                var kv = part.Split(new char[] { ':' }, 2);
                                if (kv.Length == 2)
                                {
                                    string key = kv[0].Trim().Trim('"');
                                    string val = kv[1].Trim().Trim('"');
                                    if (TryParseHexColor(val, out var col))
                                        if (!colors.ContainsKey(key)) colors.Add(key, col);
                                }
                            }
                        }
                    }
                }

                if (colors.Count > 0)
                {
                    // Merge into _classColorMap and sync chips
                    foreach (var kv in colors)
                        _classColorMap[kv.Key] = kv.Value;

                    // Ensure all classes have chips with correct color
                    foreach (var name in classes)
                    {
                        var want = GetBaseColorForClass(name);
                        var chip = FindChipByName(name);
                        if (chip == null)
                        {
                            // create chip
                            AddLabelChip(name, want);
                            chip = FindChipByName(name);
                        }
                        else
                        {
                            ApplyColorToChip(chip, want);
                        }
                    }
                }
            }
            catch { /* ignore parsing errors */ }
        }

        private static float Clamp01(float v) => (v < 0f) ? 0f : (v > 1f ? 1f : v);

        private static float SignedArea(IList<PointF> poly)
        {
            double a = 0;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                a += (double)(poly[j].X * poly[i].Y - poly[i].X * poly[j].Y);
            return (float)(0.5 * a);
        }

        private static void RemoveConsecutiveDuplicates(List<PointF> pts, float eps2 = 1e-12f)
        {
            if (pts == null || pts.Count < 2) return;
            var outPts = new List<PointF>(pts.Count);
            PointF prev = pts[0];
            outPts.Add(prev);
            for (int i = 1; i < pts.Count; i++)
            {
                float dx = pts[i].X - prev.X, dy = pts[i].Y - prev.Y;
                if (dx * dx + dy * dy > eps2) { outPts.Add(pts[i]); prev = pts[i]; }
            }
            pts.Clear(); pts.AddRange(outPts);
        }

        private static List<PointF> DownsampleByIndex(List<PointF> pts, int maxCount)
        {
            if (pts == null) return null;
            if (pts.Count <= maxCount) return pts;
            var outPts = new List<PointF>(maxCount);
            for (int i = 0; i < maxCount; i++)
            {
                int idx = (int)Math.Round((double)i * (pts.Count - 1) / (maxCount - 1));
                outPts.Add(pts[idx]);
            }
            return outPts;
        }
        private async void OnTrainClick(object sender, EventArgs e) // ← async 추가
        {
            try
            {
                // 1) 가중치 경로 결정(표준 우선, 레거시 감지)
                string weightsPath = PathHelper.ResolvePretrainedPath();

                // 2) 없으면 다이얼로그 표시
                if (!File.Exists(weightsPath))
                {
                    PretrainedWeightsDialog.ShowForMissingDefault(this, weightsPath);

                    // 3) 닫힌 뒤 다시 확인 + (중요) 잠시 대기하여 백그라운드 다운로드를 기다림
                    weightsPath = PathHelper.ResolvePretrainedPath();

                    if (!File.Exists(weightsPath))
                    {
                        // ProgressOverlay로 사용자에게 '다운로드 대기'를 보여주며 기다림 (최대 5분 예시)
                        using (var ov = new ProgressOverlay(this, "가중치 다운로드 대기", true))
                        {
                            ov.Report(0, "다운로드 준비 중...");
                            bool ok = await WaitForFileReadyAsync(
                                weightsPath,
                                TimeSpan.FromMinutes(5),
                                (pct, status) => ov.Report(pct, status)
                            );

                            if (!ok)
                            {
                                new Guna.UI2.WinForms.Guna2MessageDialog
                                {
                                    Parent = this,
                                    Caption = "학습 시작 불가",
                                    Text = "필요한 프리트레인 가중치(yolo11x-seg.pt)가 없습니다.\n" +
                                           "Download 또는 Search로 가중치를 준비한 뒤 다시 시도하세요.",
                                    Buttons = Guna.UI2.WinForms.MessageDialogButtons.OK,
                                    Icon = Guna.UI2.WinForms.MessageDialogIcon.Warning,
                                    Style = Guna.UI2.WinForms.MessageDialogStyle.Light
                                }.Show();
                                return;
                            }
                        }
                    }
                }

                // 4) 여기부터 실제 학습 로직으로 연결
                StartTraining(weightsPath);
            }
            catch (Exception ex)
            {
                new Guna.UI2.WinForms.Guna2MessageDialog
                {
                    Parent = this,
                    Caption = "오류",
                    Text = ex.Message,
                    Buttons = Guna.UI2.WinForms.MessageDialogButtons.OK,
                    Icon = Guna.UI2.WinForms.MessageDialogIcon.Error,
                    Style = Guna.UI2.WinForms.MessageDialogStyle.Light
                }.Show();
            }
        }

        private async Task<bool> WaitForFileReadyAsync(
    string path,
    TimeSpan timeout,
    Action<int, string> progress = null)
        {
            var start = DateTime.UtcNow;
            long lastSize = -1;
            DateTime lastChange = DateTime.UtcNow;

            while (DateTime.UtcNow - start < timeout)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        long size = new FileInfo(path).Length;

                        if (size > 0)
                        {
                            if (size != lastSize)
                            {
                                lastSize = size;
                                lastChange = DateTime.UtcNow;
                                progress?.Invoke(80, "다운로드 중..."); // 중반 이후는 '다운로드 중'
                            }
                            else
                            {
                                // 1초 동안 크기 변화 없으면 완료로 간주
                                if ((DateTime.UtcNow - lastChange).TotalSeconds >= 1.0)
                                {
                                    progress?.Invoke(100, "가중치 준비 완료");
                                    return true;
                                }
                            }
                        }
                    }
                }
                catch { /* 파일이 잠깐 잠겨있을 수 있음 → 무시하고 재시도 */ }

                // 시간 기반 진행률(최대 95%)
                double frac = (DateTime.UtcNow - start).TotalMilliseconds / timeout.TotalMilliseconds;
                int pct = Math.Min(95, Math.Max(5, (int)Math.Round(frac * 95)));
                progress?.Invoke(pct, "가중치 확인 중...");

                await Task.Delay(500);
            }
            return File.Exists(path); // 타임아웃 시 마지막 한 번 더 확인
        }


        private async void StartTraining(string pretrainedWeightsPath)
        {
            // =====[ 공통 경로/도구 ]===================================================
            const string baseDir = @"D:\SmartLabelingApp";
            string venvDir = Path.Combine(baseDir, ".venv");
            string pythonExe = Path.Combine(venvDir, "Scripts", "python.exe");
            string yoloExe = Path.Combine(venvDir, "Scripts", "yolo.exe");

            // =====[ STEP 0) 입력 검증 ]================================================
            if (string.IsNullOrEmpty(pretrainedWeightsPath) || !File.Exists(pretrainedWeightsPath))
            {
                new Guna.UI2.WinForms.Guna2MessageDialog
                {
                    Parent = this,
                    Caption = "Train",
                    Text = $"프리트레인 가중치(.pt)를 찾을 수 없습니다.\n경로: {pretrainedWeightsPath ?? "(null)"}",
                    Buttons = Guna.UI2.WinForms.MessageDialogButtons.OK,
                    Icon = Guna.UI2.WinForms.MessageDialogIcon.Error,
                    Style = Guna.UI2.WinForms.MessageDialogStyle.Light
                }.Show();
                return;
            }

            //// 파일명 기반 1차 체크
            //string wname = Path.GetFileName(pretrainedWeightsPath).ToLowerInvariant();
            //bool nameLooksSeg = wname.Contains("-seg.") || wname.EndsWith("seg.pt");

            //// 파이썬으로 실제 모델 task 확인 (best.pt도 정확히 판별)
            //bool probeSaysSeg = ProbeYoloTaskIsSegment(pythonExe, pretrainedWeightsPath, baseDir);

            //if (!(nameLooksSeg || probeSaysSeg))
            //{
            //    new Guna.UI2.WinForms.Guna2MessageDialog
            //    {
            //        Parent = this,
            //        Caption = "Train",
            //        Text = "세그멘테이션 학습은 세그 전용 가중치가 필요합니다.\n" +
            //               $"지금 파일: {pretrainedWeightsPath}\n(파일명에 -seg가 없으면 best.pt라도 세그 모델인지 확인해 주세요.)",
            //        Buttons = Guna.UI2.WinForms.MessageDialogButtons.OK,
            //        Icon = Guna.UI2.WinForms.MessageDialogIcon.Warning,
            //        Style = Guna.UI2.WinForms.MessageDialogStyle.Light
            //    }.Show();
            //    return;
            //}


            // =====[ STEP 1) ZIP 선택 ]=================================================

            string zipPath;
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "학습 데이터 ZIP 선택";
                ofd.Filter = "Zip Archives (*.zip)|*.zip";
                ofd.InitialDirectory = baseDir;
                ofd.CheckFileExists = true;
                ofd.CheckPathExists = true;
                ofd.Multiselect = false;
                ofd.RestoreDirectory = true;
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                zipPath = ofd.FileName;
            }

            // =====[ STEP 2) venv 준비 (있으면 건너뜀) ]================================
            using (var overlay = new ProgressOverlay(this, "환경 준비", true))
            {
                try
                {
                    overlay.Report(0, "시작 준비 중...");
                    await EnvSetup.EnsureVenvAndUltralyticsAsync(
                        baseDir, venvDir, pythonExe,
                        (pct, status) => overlay.Report(pct, status)
                    );
                    overlay.Report(100, "환경 준비 완료");
                }
                catch (Exception ex)
                {
                    new Guna.UI2.WinForms.Guna2MessageDialog
                    {
                        Parent = this,
                        Caption = "Train",
                        Text = $"가상환경 준비 실패:\n{ex.Message}",
                        Buttons = Guna.UI2.WinForms.MessageDialogButtons.OK,
                        Icon = Guna.UI2.WinForms.MessageDialogIcon.Error,
                        Style = Guna.UI2.WinForms.MessageDialogStyle.Light
                    }.Show();
                    return;
                }
            }

            // =====[ STEP 3) ZIP 해제 + 데이터셋 검증 ]=================================
            string extractRoot = Path.Combine(baseDir, "Result"); // Result 폴더를 재생성
            string dataYamlPath = null;
            string datasetRoot = null;
            int trainImg = 0, trainLbl = 0, valImg = 0, valLbl = 0;

            using (var overlay = new ProgressOverlay(this, "데이터 준비", true))
            {
                try
                {
                    overlay.Report(0, "Result 폴더 정리...");
                    await ZipDatasetUtils.ExtractZipWithProgressAsync(
                        zipPath, extractRoot, (pct, msg) => overlay.Report(pct, msg));

                    overlay.Report(86, "data.yaml 탐색...");
                    dataYamlPath = DataYamlPatcher.FindDataYaml(extractRoot);
                    if (string.IsNullOrEmpty(dataYamlPath))
                        throw new Exception("data.yaml을 찾을 수 없습니다. ZIP 구조를 확인하세요.");

                    datasetRoot = Path.GetDirectoryName(dataYamlPath) ?? extractRoot;

                    overlay.Report(90, "디렉토리 구조 검증...");
                    DataYamlPatcher.ValidateRequiredDirs(datasetRoot);

                    overlay.Report(94, "파일 개수 점검...");
                    (trainImg, trainLbl) = DataYamlPatcher.CountPair(datasetRoot, @"images\train", @"labels\train");
                    (valImg, valLbl) = DataYamlPatcher.CountPair(datasetRoot, @"images\val", @"labels\val");

                    if (trainImg == 0 || trainLbl == 0) throw new Exception("train 이미지/라벨이 비어 있습니다.");
                    if (valImg == 0 || valLbl == 0) throw new Exception("val 이미지/라벨이 비어 있습니다.");

                    DataYamlPatcher.FixDataYamlForExtractedDataset(dataYamlPath, datasetRoot);
                    overlay.Report(100, "데이터 준비 완료");
                }
                catch (Exception ex)
                {
                    new Guna.UI2.WinForms.Guna2MessageDialog
                    {
                        Parent = this,
                        Caption = "Train",
                        Text = $"데이터 준비 실패:\n{ex.Message}",
                        Buttons = Guna.UI2.WinForms.MessageDialogButtons.OK,
                        Icon = Guna.UI2.WinForms.MessageDialogIcon.Error,
                        Style = Guna.UI2.WinForms.MessageDialogStyle.Light
                    }.Show();
                    return;
                }
            }

            // =====[ STEP 4) 학습 실행 ]================================================
            string projectDir = Path.Combine(baseDir, "runs");
            Directory.CreateDirectory(projectDir);
            string runName = "finetune_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string bestOut = Path.Combine(projectDir, runName, "weights", "best.pt");

            int epochs = 20;
            int imgsz = 1024;
            int batch = 8;
            string device = "0";      // 첫 번째 GPU

            string args = string.Join(" ",
    "segment", "train",
    "model=" + YoloCli.Quote(pretrainedWeightsPath),
    "data=" + YoloCli.Quote(dataYamlPath),
    "epochs=" + epochs,
    "imgsz=" + imgsz,
    "batch=" + batch,
    "device=" + device,
    "project=" + YoloCli.Quote(projectDir),
    "retina_masks=True",
    "overlap_mask=True",
    "name=" + YoloCli.Quote(runName)
);


            string bestCopy = null;
            string onnxPath = null;

            using (var overlay2 = new ProgressOverlay(this, "학습 실행", true))
            {
                try
                {
                    overlay2.Report(0, "YOLO 준비 중...");
                    var cli = YoloCli.GetYoloCli(yoloExe, pythonExe);

                    int exit = await YoloTrainer.RunYoloTrainWithEpochProgressAsync(cli.fileName, cli.argumentsPrefix + " " + args, baseDir, (pct, status) => overlay2.Report(pct, status), 0, 96);
                    if (exit != 0)
                        throw new Exception($"YOLO 학습 프로세스가 실패했습니다. (exit={exit})");

                    // 에폭 종료 후 남은 4%를 산출물 수집/복사에 사용
                    overlay2.Report(98, "결과 수집...");
                    if (!File.Exists(bestOut))
                        throw new Exception($"best.pt를 찾을 수 없습니다.\n경로: {bestOut}");
                    string saveTo = Path.Combine(baseDir, "weights", "finetuned", runName);
                    Directory.CreateDirectory(saveTo);
                    bestCopy = Path.Combine(saveTo, "best.pt");
                    File.Copy(bestOut, bestCopy, true);

                    overlay2.Report(100, "학습 완료");
                }
                catch (Exception ex)
                {
                    new Guna.UI2.WinForms.Guna2MessageDialog
                    {
                        Parent = this,
                        Caption = "Train",
                        Text = $"학습 실행 실패:\n{ex.Message}",
                        Buttons = Guna.UI2.WinForms.MessageDialogButtons.OK,
                        Icon = Guna.UI2.WinForms.MessageDialogIcon.Error,
                        Style = Guna.UI2.WinForms.MessageDialogStyle.Light
                    }.Show();
                    return;
                }
            }

            // =====[ STEP 5) ONNX 내보내기 ]============================================
            if (!string.IsNullOrEmpty(bestCopy) && File.Exists(bestCopy))
            {
                using (var ov = new ProgressOverlay(this, "Export: ONNX", true))
                {
                    try
                    {
                        ov.Report(2, "onnx/onnxsim 의존성 확인...");
                        var pipEnv = EnvSetup.GetPipEnv(baseDir);
                        int ec = ProcessRunner.RunProcessProgress(
                            pythonExe,
                            "-m pip install --upgrade --no-cache-dir --prefer-binary onnx onnxsim --timeout 180 --retries 2",
                            baseDir, 2, 20, (p, s) => ov.Report(p, s), "pip (onnx)", pipEnv);
                        if (ec != 0) throw new Exception("onnx/onnxsim 설치 실패");

                        ov.Report(22, "ONNX 변환 준비...");
                        var cli = YoloCli.GetYoloCli(yoloExe, pythonExe);
                        string exportArgs = string.Join(" ",
                            "export",
                            "model=" + YoloCli.Quote(bestCopy),
                            "format=onnx",
                            "opset=12",
                            "dynamic=True",
                            "simplify=True",
                            "imgsz=" + imgsz
                        );

                        ov.Report(25, "ONNX 변환 중...");
                        ec = ProcessRunner.RunProcessProgress(
                            cli.fileName, cli.argumentsPrefix + " " + exportArgs, baseDir,
                            25, 95, (p, s) => ov.Report(p, "ONNX 변환 중..."), "yolo export", null);
                        if (ec != 0) throw new Exception("ONNX 변환 실패");

                        ov.Report(97, "결과 확인...");
                        string searchStart = Path.GetDirectoryName(bestCopy) ?? baseDir;
                        onnxPath = Directory.EnumerateFiles(searchStart, "*.onnx", SearchOption.AllDirectories)
                                            .OrderByDescending(p => new FileInfo(p).LastWriteTimeUtc)
                                            .FirstOrDefault();

                        if (string.IsNullOrEmpty(onnxPath) || !File.Exists(onnxPath))
                            throw new Exception(".onnx 파일을 찾지 못했습니다.");

                        ov.Report(100, "Export 완료");
                    }
                    catch (Exception ex)
                    {
                        new Guna.UI2.WinForms.Guna2MessageDialog
                        {
                            Parent = this,
                            Caption = "Export",
                            Text = "ONNX 내보내기 실패:\n" + ex.Message,
                            Buttons = Guna.UI2.WinForms.MessageDialogButtons.OK,
                            Icon = Guna.UI2.WinForms.MessageDialogIcon.Error,
                            Style = Guna.UI2.WinForms.MessageDialogStyle.Light
                        }.Show();
                    }
                }
            }

            // =====[ 완료 안내 ]========================================================
            {
                var msg = "학습이 완료되었습니다.\n\n" +
                          $"runs 경로: {Path.Combine(projectDir, runName)}" +
                          (string.IsNullOrEmpty(bestCopy) ? "" : $"\nPT 복사본: {bestCopy}") +
                          (string.IsNullOrEmpty(onnxPath) ? "" : $"\nONNX: {onnxPath}");

                new Guna.UI2.WinForms.Guna2MessageDialog
                {
                    Parent = this,
                    Caption = "Train",
                    Text = msg,
                    Buttons = Guna.UI2.WinForms.MessageDialogButtons.OK,
                    Icon = Guna.UI2.WinForms.MessageDialogIcon.Information,
                    Style = Guna.UI2.WinForms.MessageDialogStyle.Light
                }.Show();
            }
        }

        // C# 7.3 호환, 간단 프로브 함수
        private static bool ProbeYoloTaskIsSegment(string pythonExe, string modelPath, string workdir)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = pythonExe;
                psi.Arguments = "-c \"from ultralytics import YOLO; import sys; m=YOLO(sys.argv[1]); print(m.task)\" \"" + modelPath + "\"";
                psi.WorkingDirectory = workdir;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string _ = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    return p.ExitCode == 0 && stdout.Trim().ToLowerInvariant().Contains("segment");
                }
            }
            catch { return false; }
        }


        /// <summary>닫힌 폴리곤(시작=끝이 아니어도 됨)을 둘레 길이 균등으로 target개 리샘플.</summary>
        private static List<PointF> ResampleClosedByArcLen(List<PointF> closed, int target)
        {
            if (closed == null || closed.Count < 3) return closed ?? new List<PointF>();
            var poly = new List<PointF>(closed);
            if (poly[0] != poly[poly.Count - 1]) poly.Add(poly[0]); // 닫기

            int n = poly.Count;
            var cum = new double[n];
            cum[0] = 0;
            double total = 0;
            for (int i = 1; i < n; i++)
            {
                double dx = poly[i].X - poly[i - 1].X, dy = poly[i].Y - poly[i - 1].Y;
                double seg = Math.Sqrt(dx * dx + dy * dy);
                total += seg; cum[i] = total;
            }
            if (total <= 1e-9) return new List<PointF> { poly[0] };

            target = Math.Max(8, target);
            var outPts = new List<PointF>(target);
            int segIdx = 1;
            for (int i = 0; i < target; i++)
            {
                double s = (i * total) / target;
                while (segIdx < n && cum[segIdx] < s) segIdx++;
                if (segIdx >= n) segIdx = n - 1;

                var a = poly[segIdx - 1];
                var b = poly[segIdx];
                double segStart = cum[segIdx - 1];
                double segLen = Math.Max(1e-9, cum[segIdx] - segStart);
                double t = (s - segStart) / segLen;

                outPts.Add(new PointF(
                    (float)(a.X + (b.X - a.X) * t),
                    (float)(a.Y + (b.Y - a.Y) * t)
                ));
            }
            return outPts;
        }

        /// <summary>GraphicsPath에서 가장 큰 폐곡선(외곽)만 추출, 시작=끝 제거.</summary>
        private static List<PointF> GetLargestClosedOutline(GraphicsPath gp)
        {
            if (gp == null) return null;
            gp.Flatten();
            var pts = gp.PathPoints;
            var types = gp.PathTypes;
            if (pts == null || types == null || pts.Length < 3) return null;

            byte mask = (byte)PathPointType.PathTypeMask;
            int start = 0;
            List<PointF> best = null;
            double bestArea = 0;

            for (int i = 0; i < types.Length; i++)
            {
                bool isStart = ((types[i] & mask) == (byte)PathPointType.Start);
                bool isLast = (i == types.Length - 1);
                bool nextIsStart = !isLast && ((types[i + 1] & mask) == (byte)PathPointType.Start);

                if (isStart) start = i;
                if (isLast || nextIsStart)
                {
                    int len = i - start + 1;
                    if (len >= 3)
                    {
                        var seg = new List<PointF>(len + 1);
                        for (int k = 0; k < len; k++) seg.Add(pts[start + k]);
                        if (seg[0] != seg[seg.Count - 1]) seg.Add(seg[0]);

                        // 면적
                        double area = 0;
                        for (int a = 0, b = seg.Count - 1; a < seg.Count; b = a++)
                            area += (double)(seg[b].X * seg[a].Y - seg[a].X * seg[b].Y);
                        area = Math.Abs(area) * 0.5;

                        if (area > bestArea) { bestArea = area; best = seg; }
                    }
                }
            }
            if (best == null) return null;
            if (best.Count >= 2 && best[0] == best[best.Count - 1]) best.RemoveAt(best.Count - 1);
            return best;
        }

        /// <summary>라인 한 줄 추가. 폴리곤 정리(CCW/중복/정규화). 성공 시 true.</summary>
        private static bool AppendSegLine(List<string> lines, int cls, IList<PointF> ptsImg, int W, int H, IFormatProvider ci)
        {
            if (ptsImg == null || ptsImg.Count < 3) return false;

            var poly = new List<PointF>(ptsImg);

            // 닫힘점 제거
            if (poly.Count >= 2)
            {
                var a = poly[0]; var b = poly[poly.Count - 1];
                if (Math.Abs(a.X - b.X) < 1e-6f && Math.Abs(a.Y - b.Y) < 1e-6f)
                    poly.RemoveAt(poly.Count - 1);
            }
            RemoveConsecutiveDuplicates(poly);
            if (poly.Count < 3) return false;

            // CCW 통일
            if (SignedArea(poly) < 0) poly.Reverse();

            var sb = new System.Text.StringBuilder();
            sb.Append(cls);
            for (int i = 0; i < poly.Count; i++)
            {
                float nx = Clamp01(poly[i].X / W);
                float ny = Clamp01(poly[i].Y / H);
                sb.Append(' ').Append(nx.ToString(ci)).Append(' ').Append(ny.ToString(ci));
            }
            lines.Add(sb.ToString());
            return true;
        }
        // ===== end helpers =====

        private void WriteYoloLabelForCurrentImage(string labelFilePath, List<string> classes)
        {
            var img = _canvas.Image;
            int W = img.Width, H = img.Height;
            var ci = System.Globalization.CultureInfo.InvariantCulture;

            // 설정값
            int CIRCLE_SAMPLES_DEFAULT = Math.Max(8, EditorUIConfig.CircleSegVertexCount);
            int BRUSH_MAX_PTS = 256;  // 브러시 외곽 폴리곤 최대 정점 수
            int POLY_MAX_PTS = 512;  // 폴리곤 상한(초과 시 인덱스 다운샘플)

            var lines = new List<string>();

            for (int i = 0; i < _canvas.Shapes.Count; i++)
            {
                var s = _canvas.Shapes[i];
                string lbl = GetShapeLabel(s);
                int cls = GetOrAppendClassId(lbl, classes);

                // Rectangle → 4점 폴리곤
                if (s is RectangleShape rs)
                {
                    var r = rs.RectImg;
                    var rectPoly = new List<PointF>
            {
                new PointF(r.Left,  r.Top),
                new PointF(r.Right, r.Top),
                new PointF(r.Right, r.Bottom),
                new PointF(r.Left,  r.Bottom),
            };
                    if (rectPoly.Count > POLY_MAX_PTS) rectPoly = DownsampleByIndex(rectPoly, POLY_MAX_PTS);
                    AppendSegLine(lines, cls, rectPoly, W, H, ci);
                    continue;
                }

                // Circle → 정다각형 근사 (편집 VertexCount 우선)
                if (s is CircleShape cs)
                {
                    var r = cs.RectImg;
                    float cx = r.X + r.Width * 0.5f;
                    float cy = r.Y + r.Height * 0.5f;
                    float rad = Math.Max(r.Width, r.Height) * 0.5f;

                    int n = (cs.VertexCount > 0) ? cs.VertexCount : CIRCLE_SAMPLES_DEFAULT;
                    n = Math.Max(8, Math.Min(n, POLY_MAX_PTS));

                    var pts = new List<PointF>(n);
                    for (int k = 0; k < n; k++)
                    {
                        double th = 2.0 * Math.PI * k / n; // CCW
                        pts.Add(new PointF(
                            cx + (float)(rad * Math.Cos(th)),
                            cy + (float)(rad * Math.Sin(th))
                        ));
                    }
                    AppendSegLine(lines, cls, pts, W, H, ci);
                    continue;
                }

                // Triangle / Polygon → 꼭짓점 그대로 (상한 초과 시 다운샘플)
                if (s is TriangleShape ts && ts.PointsImg != null && ts.PointsImg.Count >= 3)
                {
                    var pts = ts.PointsImg;
                    if (pts.Count > POLY_MAX_PTS) pts = DownsampleByIndex(new List<PointF>(pts), POLY_MAX_PTS);
                    AppendSegLine(lines, cls, pts, W, H, ci);
                    continue;
                }
                if (s is PolygonShape ps && ps.PointsImg != null && ps.PointsImg.Count >= 3)
                {
                    var pts = ps.PointsImg;
                    if (pts.Count > POLY_MAX_PTS) pts = DownsampleByIndex(new List<PointF>(pts), POLY_MAX_PTS);
                    AppendSegLine(lines, cls, pts, W, H, ci);
                    continue;
                }

                // BrushStroke → 외곽(가장 큰 폐곡선)을 호길이 균등 리샘플 후 저장
                if (s is BrushStrokeShape bs)
                {
                    using (var gp = bs.GetAreaPathImgClone())
                    {
                        var outer = GetLargestClosedOutline(gp);
                        if (outer != null && outer.Count >= 3)
                        {
                            int target = Math.Min(BRUSH_MAX_PTS, Math.Max(32, outer.Count));
                            var res = ResampleClosedByArcLen(outer, target);
                            AppendSegLine(lines, cls, res, W, H, ci);
                        }
                    }
                    continue;
                }

                // 기타 → bbox를 4점 폴리곤으로 저장(세그 형식 일관)
                var b = s.GetBoundsImg();
                if (!b.IsEmpty)
                {
                    var boxPoly = new List<PointF>
            {
                new PointF(b.Left,  b.Top),
                new PointF(b.Right, b.Top),
                new PointF(b.Right, b.Bottom),
                new PointF(b.Left,  b.Bottom),
            };
                    AppendSegLine(lines, cls, boxPoly, W, H, ci);
                }
            }

            System.IO.File.WriteAllLines(labelFilePath, lines, System.Text.Encoding.ASCII);
        }


        private int GetOrAppendClassId(string label, List<string> classes)
        {
            if (string.IsNullOrWhiteSpace(label)) label = "Default";
            int idx = classes.IndexOf(label);
            if (idx < 0)
            {
                classes.Add(label);
                idx = classes.Count - 1;
            }
            return idx;
        }

        private string GetShapeLabel(object shape)
        {
            // 도형에 LabelName 또는 Name 속성이 있는 구조(현재 베이스와 일치)
            var t = shape.GetType();
            var p = t.GetProperty("LabelName") ?? t.GetProperty("Name");
            if (p != null)
            {
                var v = p.GetValue(shape, null);
                if (v != null) return v.ToString();
            }
            return "Default";
        }

        #endregion
    }
}