// GifCreator - 윈도우 캡처도구 스타일의 화면 영역 GIF 녹화 도구
// 최대 30초, 최대 15fps. 외부 의존성 없음 (.NET Framework 4.x 내장 기능만 사용)
//
// 빌드: build.bat 참고 (Windows 내장 csc.exe로 컴파일)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("GIF Creator")]
[assembly: AssemblyProduct("GifCreator")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace GifCreatorApp
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--selftest")
            {
                Environment.Exit(SelfTest.Run());
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    // ---------------------------------------------------------------
    // 메인 창
    // ---------------------------------------------------------------
    class MainForm : Form
    {
        const int WM_HOTKEY = 0x0312;
        const int HOTKEY_ID = 0xB001;

        NumericUpDown numFps;
        NumericUpDown numSec;
        CheckBox chkCursor;
        CheckBox chkHide;
        Button btnMain;
        Label lblStatus;
        LinkLabel lnkFolder;

        Recorder recorder;
        BorderForm borderForm;
        bool isRecording;
        bool isEncoding;
        string lastSavedPath;

        public MainForm()
        {
            Text = "GIF Creator";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(360, 232);

            var lblFps = new Label();
            lblFps.Text = "초당 프레임 (FPS)";
            lblFps.SetBounds(16, 18, 120, 20);
            Controls.Add(lblFps);

            numFps = new NumericUpDown();
            numFps.Minimum = 1;
            numFps.Maximum = 15;
            numFps.Value = 10;
            numFps.SetBounds(140, 15, 60, 24);
            Controls.Add(numFps);

            var lblSec = new Label();
            lblSec.Text = "최대 길이 (초)";
            lblSec.SetBounds(16, 50, 120, 20);
            Controls.Add(lblSec);

            numSec = new NumericUpDown();
            numSec.Minimum = 1;
            numSec.Maximum = 30;
            numSec.Value = 30;
            numSec.SetBounds(140, 47, 60, 24);
            Controls.Add(numSec);

            chkCursor = new CheckBox();
            chkCursor.Text = "마우스 커서 포함";
            chkCursor.Checked = true;
            chkCursor.SetBounds(16, 82, 160, 22);
            Controls.Add(chkCursor);

            chkHide = new CheckBox();
            chkHide.Text = "녹화 중 이 창 숨기기";
            chkHide.SetBounds(180, 82, 170, 22);
            Controls.Add(chkHide);

            btnMain = new Button();
            btnMain.Text = "● 영역 선택 후 녹화 시작";
            btnMain.SetBounds(16, 114, 328, 40);
            btnMain.Click += OnMainButtonClick;
            Controls.Add(btnMain);

            lblStatus = new Label();
            lblStatus.Text = "대기 중 — 캡처도구처럼 드래그로 영역을 지정합니다.";
            lblStatus.SetBounds(16, 164, 328, 38);
            Controls.Add(lblStatus);

            lnkFolder = new LinkLabel();
            lnkFolder.Text = "저장 폴더 열기";
            lnkFolder.SetBounds(16, 204, 328, 20);
            lnkFolder.Visible = false;
            lnkFolder.LinkClicked += delegate
            {
                if (lastSavedPath != null && File.Exists(lastSavedPath))
                    Process.Start("explorer.exe", "/select,\"" + lastSavedPath + "\"");
            };
            Controls.Add(lnkFolder);
        }

        void OnMainButtonClick(object sender, EventArgs e)
        {
            if (isRecording)
            {
                StopRecording();
                return;
            }
            if (isEncoding) return;
            BeginRegionSelection();
        }

        void BeginRegionSelection()
        {
            Hide();
            Application.DoEvents();
            Thread.Sleep(250); // 메인 창이 화면에서 사라진 뒤 스크린샷을 찍기 위한 대기

            Rectangle region = Rectangle.Empty;
            using (var sel = new RegionSelectForm())
            {
                if (sel.ShowDialog() == DialogResult.OK)
                    region = sel.SelectedRegion;
            }

            if (region.Width < 10 || region.Height < 10)
            {
                Show();
                Activate();
                SetStatus("영역 선택이 취소되었습니다.");
                return;
            }

            StartRecording(region);
        }

        void StartRecording(Rectangle region)
        {
            borderForm = new BorderForm(region);
            borderForm.Show();

            isRecording = true;
            lnkFolder.Visible = false;
            btnMain.Text = "■ 녹화 중지 (F9)";
            numFps.Enabled = numSec.Enabled = chkCursor.Enabled = chkHide.Enabled = false;

            if (!chkHide.Checked)
            {
                TopMost = true;
                Show();
            }
            NativeMethods.RegisterHotKey(Handle, HOTKEY_ID, 0, (int)Keys.F9);

            recorder = new Recorder(region, (int)numFps.Value, (int)numSec.Value * 1000, chkCursor.Checked);
            recorder.Progress += delegate(double sec, int frames)
            {
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        SetStatus(string.Format("녹화 중... {0:0.0}초 / {1} 프레임  (F9로 중지)", sec, frames));
                    });
                }
                catch (InvalidOperationException) { }
            };
            recorder.Finished += delegate(RecordResult result)
            {
                try
                {
                    BeginInvoke((Action)delegate { OnRecordingFinished(result); });
                }
                catch (InvalidOperationException) { }
            };
            recorder.Start();
            SetStatus("녹화 중... (F9로 중지)");
        }

        void StopRecording()
        {
            if (recorder != null) recorder.RequestStop();
        }

        void OnRecordingFinished(RecordResult result)
        {
            isRecording = false;
            NativeMethods.UnregisterHotKey(Handle, HOTKEY_ID);
            if (borderForm != null) { borderForm.Close(); borderForm = null; }

            TopMost = false;
            Show();
            Activate();
            btnMain.Text = "● 영역 선택 후 녹화 시작";
            numFps.Enabled = numSec.Enabled = chkCursor.Enabled = chkHide.Enabled = true;

            if (result.Error != null)
            {
                SetStatus("녹화 실패: " + result.Error);
                return;
            }
            if (result.Frames.Count == 0)
            {
                SetStatus("캡처된 프레임이 없습니다.");
                return;
            }

            var dlg = new SaveFileDialog();
            dlg.Filter = "GIF 이미지 (*.gif)|*.gif";
            dlg.FileName = "capture_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".gif";
            dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (dlg.ShowDialog(this) != DialogResult.OK)
            {
                SetStatus("저장이 취소되어 녹화 내용을 버렸습니다.");
                return;
            }

            EncodeAsync(result, dlg.FileName);
        }

        void EncodeAsync(RecordResult result, string path)
        {
            isEncoding = true;
            btnMain.Enabled = false;
            SetStatus(string.Format("GIF 인코딩 중... ({0} 프레임)", result.Frames.Count));

            var worker = new Thread(delegate()
            {
                string error = null;
                long size = 0;
                try
                {
                    GifWriter.Write(path, result.Frames, result.TimesMs, result.Fps);
                    size = new FileInfo(path).Length;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        isEncoding = false;
                        btnMain.Enabled = true;
                        if (error != null)
                        {
                            SetStatus("인코딩 실패: " + error);
                        }
                        else
                        {
                            lastSavedPath = path;
                            lnkFolder.Visible = true;
                            SetStatus(string.Format("저장 완료: {0} ({1:0.0} MB, {2} 프레임)",
                                Path.GetFileName(path), size / 1048576.0, result.Frames.Count));
                        }
                    });
                }
                catch (InvalidOperationException) { }
            });
            worker.IsBackground = true;
            worker.Start();
        }

        void SetStatus(string text)
        {
            lblStatus.Text = text;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && (int)m.WParam == HOTKEY_ID && isRecording)
            {
                StopRecording();
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (recorder != null) recorder.RequestStop();
            base.OnFormClosing(e);
        }
    }

    // ---------------------------------------------------------------
    // 영역 선택 오버레이 (윈도우 캡처도구 스타일)
    // 화면을 정지 화면으로 얼린 뒤 어둡게 깔고, 드래그한 영역만 밝게 보여준다.
    // ---------------------------------------------------------------
    class RegionSelectForm : Form
    {
        readonly Rectangle virtualScreen;
        readonly Bitmap screenshot;
        readonly Bitmap dimmed;
        Point dragStart;
        Rectangle selection = Rectangle.Empty;
        bool dragging;

        public Rectangle SelectedRegion { get; private set; }

        public RegionSelectForm()
        {
            virtualScreen = SystemInformation.VirtualScreen;

            screenshot = new Bitmap(virtualScreen.Width, virtualScreen.Height, PixelFormat.Format32bppRgb);
            using (var g = Graphics.FromImage(screenshot))
                g.CopyFromScreen(virtualScreen.X, virtualScreen.Y, 0, 0, virtualScreen.Size);

            dimmed = new Bitmap(screenshot);
            using (var g = Graphics.FromImage(dimmed))
            using (var overlay = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                g.FillRectangle(overlay, 0, 0, dimmed.Width, dimmed.Height);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = virtualScreen;
            TopMost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;
            KeyPreview = true;
            DoubleBuffered = true;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                dragStart = e.Location;
                selection = new Rectangle(e.Location, Size.Empty);
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging)
            {
                selection = Normalize(dragStart, e.Location);
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && dragging)
            {
                dragging = false;
                selection = Normalize(dragStart, e.Location);
                if (selection.Width >= 10 && selection.Height >= 10)
                {
                    // 클라이언트 좌표 → 실제 화면 좌표
                    SelectedRegion = new Rectangle(
                        selection.X + virtualScreen.X,
                        selection.Y + virtualScreen.Y,
                        selection.Width, selection.Height);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    selection = Rectangle.Empty;
                    Invalidate();
                }
            }
            base.OnMouseUp(e);
        }

        static Rectangle Normalize(Point a, Point b)
        {
            return new Rectangle(
                Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
                Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImageUnscaled(dimmed, 0, 0);

            if (selection.Width > 0 && selection.Height > 0)
            {
                // 선택 영역은 원본 밝기로
                g.DrawImage(screenshot, selection, selection, GraphicsUnit.Pixel);
                using (var pen = new Pen(Color.Red, 2f))
                    g.DrawRectangle(pen, selection);

                string sizeText = selection.Width + " × " + selection.Height;
                DrawTag(g, sizeText,
                    selection.X,
                    selection.Y >= 28 ? selection.Y - 26 : selection.Bottom + 6);
            }
            else
            {
                string hint = "드래그하여 녹화할 영역을 선택하세요  ·  ESC 취소";
                var size = g.MeasureString(hint, Font);
                var screen = Screen.FromPoint(Cursor.Position).Bounds;
                DrawTag(g, hint,
                    (int)(screen.X - virtualScreen.X + (screen.Width - size.Width) / 2),
                    screen.Y - virtualScreen.Y + 40);
            }
        }

        void DrawTag(Graphics g, string text, int x, int y)
        {
            var size = g.MeasureString(text, Font);
            var rect = new Rectangle(x, y, (int)size.Width + 12, (int)size.Height + 6);
            using (var bg = new SolidBrush(Color.FromArgb(220, 32, 32, 32)))
                g.FillRectangle(bg, rect);
            g.DrawString(text, Font, Brushes.White, x + 6, y + 3);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                screenshot.Dispose();
                dimmed.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ---------------------------------------------------------------
    // 녹화 중 표시되는 빨간 테두리 (클릭 통과, 캡처 영역 바깥에 그려짐)
    // ---------------------------------------------------------------
    class BorderForm : Form
    {
        const int BORDER = 3;

        public BorderForm(Rectangle captureRegion)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            Bounds = Rectangle.Inflate(captureRegion, BORDER, BORDER);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x20 | 0x80 | 0x8000000; // WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var pen = new Pen(Color.Red, BORDER * 2f))
                e.Graphics.DrawRectangle(pen, ClientRectangle); // 펜 절반은 창 바깥으로 잘려나가 3px 테두리가 됨
        }
    }

    // ---------------------------------------------------------------
    // 녹화 결과
    // ---------------------------------------------------------------
    class RecordResult
    {
        public List<byte[]> Frames = new List<byte[]>(); // PNG 압축된 프레임
        public List<int> TimesMs = new List<int>();      // 각 프레임의 캡처 시각 (ms)
        public int Fps;
        public string Error;
    }

    // ---------------------------------------------------------------
    // 백그라운드 캡처 스레드
    // ---------------------------------------------------------------
    class Recorder
    {
        const long MEMORY_LIMIT_BYTES = 800L * 1024 * 1024;

        readonly Rectangle region;
        readonly int fps;
        readonly int maxMs;
        readonly bool includeCursor;
        volatile bool stopRequested;

        public event Action<double, int> Progress;
        public event Action<RecordResult> Finished;

        public Recorder(Rectangle region, int fps, int maxMs, bool includeCursor)
        {
            this.region = region;
            this.fps = fps;
            this.maxMs = maxMs;
            this.includeCursor = includeCursor;
        }

        public void RequestStop()
        {
            stopRequested = true;
        }

        public void Start()
        {
            var t = new Thread(Run);
            t.IsBackground = true;
            t.Priority = ThreadPriority.AboveNormal;
            t.Start();
        }

        void Run()
        {
            var result = new RecordResult();
            result.Fps = fps;
            double interval = 1000.0 / fps;
            int maxFrames = (int)Math.Ceiling(fps * maxMs / 1000.0);
            long totalBytes = 0;

            try
            {
                using (var bmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppRgb))
                using (var g = Graphics.FromImage(bmp))
                {
                    var sw = Stopwatch.StartNew();
                    int tick = 0;
                    while (!stopRequested && result.Frames.Count < maxFrames)
                    {
                        double target = tick * interval;
                        if (target >= maxMs) break;

                        // 다음 캡처 시점까지 대기 (중지 요청을 빠르게 반영하도록 짧게 쪼개서)
                        while (!stopRequested)
                        {
                            double wait = target - sw.Elapsed.TotalMilliseconds;
                            if (wait <= 0.5) break;
                            Thread.Sleep(wait > 15 ? 15 : (int)Math.Max(1, wait));
                        }
                        if (stopRequested) break;

                        int t = (int)sw.Elapsed.TotalMilliseconds;
                        g.CopyFromScreen(region.X, region.Y, 0, 0, region.Size);
                        if (includeCursor)
                            NativeMethods.DrawCursor(g, region);

                        using (var ms = new MemoryStream())
                        {
                            bmp.Save(ms, ImageFormat.Png);
                            var bytes = ms.ToArray();
                            result.Frames.Add(bytes);
                            result.TimesMs.Add(t);
                            totalBytes += bytes.Length;
                        }

                        if (totalBytes > MEMORY_LIMIT_BYTES)
                        {
                            result.Error = null; // 한도 도달 시 그때까지의 프레임으로 저장 진행
                            break;
                        }

                        if (Progress != null)
                            Progress(sw.Elapsed.TotalSeconds, result.Frames.Count);

                        // 캡처가 밀렸으면 놓친 틱은 건너뛴다 (타임스탬프 기반 딜레이로 재생 속도는 유지됨)
                        int next = tick + 1;
                        int caughtUp = (int)(sw.Elapsed.TotalMilliseconds / interval) + 1;
                        tick = Math.Max(next, caughtUp);
                    }
                }
            }
            catch (Exception ex)
            {
                if (result.Frames.Count == 0)
                    result.Error = ex.Message;
            }

            if (Finished != null)
                Finished(result);
        }
    }

    // ---------------------------------------------------------------
    // GIF 인코딩: WIC(GifBitmapEncoder)로 인코딩한 뒤 바이트 스트림을 직접 수정해
    // 프레임 딜레이(GCE)와 무한 반복(NETSCAPE2.0)을 삽입한다.
    // WIC 인코더는 프레임 메타데이터의 딜레이를 기록하지 않기 때문에 후처리가 필요하다.
    // ---------------------------------------------------------------
    static class GifWriter
    {
        public static void Write(string path, List<byte[]> pngFrames, List<int> timesMs, int fps)
        {
            var encoder = new System.Windows.Media.Imaging.GifBitmapEncoder();
            var streams = new List<MemoryStream>();
            try
            {
                foreach (var png in pngFrames)
                {
                    var ms = new MemoryStream(png);
                    streams.Add(ms);
                    var dec = System.Windows.Media.Imaging.BitmapDecoder.Create(
                        ms,
                        System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                        System.Windows.Media.Imaging.BitmapCacheOption.None);
                    encoder.Frames.Add(dec.Frames[0]);
                }

                byte[] raw;
                using (var outMs = new MemoryStream())
                {
                    encoder.Save(outMs);
                    raw = outMs.ToArray();
                }

                var delays = BuildDelays(timesMs, fps);
                File.WriteAllBytes(path, PatchGif(raw, delays));
            }
            finally
            {
                foreach (var s in streams) s.Dispose();
            }
        }

        // 실제 캡처 타임스탬프로부터 프레임별 딜레이(1/100초 단위)를 계산한다.
        // 누적 반올림 방식이라 프레임이 밀리거나 건너뛰어도 전체 재생 시간이 실제와 일치한다.
        internal static ushort[] BuildDelays(List<int> timesMs, int fps)
        {
            int n = timesMs.Count;
            var delays = new ushort[n];
            int fallback = Math.Max(2, (int)Math.Round(100.0 / fps));
            for (int i = 0; i < n; i++)
            {
                int d;
                if (i < n - 1)
                    d = (int)Math.Round(timesMs[i + 1] / 10.0) - (int)Math.Round(timesMs[i] / 10.0);
                else
                    d = fallback;
                delays[i] = (ushort)Math.Max(2, Math.Min(65535, d));
            }
            return delays;
        }

        // GIF 블록 구조를 순회하며 NETSCAPE2.0 루프 확장을 삽입하고
        // 각 이미지 앞의 Graphic Control Extension에 딜레이를 기록한다.
        internal static byte[] PatchGif(byte[] src, ushort[] delays)
        {
            var outB = new List<byte>(src.Length + 8 * delays.Length + 19);
            int pos = 13; // header(6) + logical screen descriptor(7)
            for (int i = 0; i < pos; i++) outB.Add(src[i]);

            byte packed = src[10];
            if ((packed & 0x80) != 0) // global color table
            {
                int gctSize = 3 * (1 << ((packed & 0x07) + 1));
                for (int i = 0; i < gctSize; i++) outB.Add(src[pos + i]);
                pos += gctSize;
            }

            // NETSCAPE2.0 무한 반복 확장
            outB.AddRange(new byte[] {
                0x21, 0xFF, 0x0B,
                (byte)'N', (byte)'E', (byte)'T', (byte)'S', (byte)'C', (byte)'A', (byte)'P', (byte)'E',
                (byte)'2', (byte)'.', (byte)'0',
                0x03, 0x01, 0x00, 0x00, 0x00
            });

            int frameIndex = 0;
            bool pendingGce = false;

            while (pos < src.Length)
            {
                byte b = src[pos];
                if (b == 0x3B) // trailer
                {
                    outB.Add(b);
                    break;
                }
                if (b == 0x21) // extension
                {
                    byte label = src[pos + 1];
                    if (label == 0xF9) // graphic control extension: 딜레이만 덮어쓰기
                    {
                        var gce = new byte[8];
                        Array.Copy(src, pos, gce, 0, 8);
                        if (frameIndex < delays.Length)
                        {
                            gce[4] = (byte)(delays[frameIndex] & 0xFF);
                            gce[5] = (byte)((delays[frameIndex] >> 8) & 0xFF);
                        }
                        outB.AddRange(gce);
                        pos += 8;
                        pendingGce = true;
                    }
                    else // 그 외 확장 블록은 그대로 복사
                    {
                        outB.Add(src[pos]); outB.Add(src[pos + 1]);
                        pos += 2;
                        pos = CopySubBlocks(src, pos, outB);
                    }
                }
                else if (b == 0x2C) // image descriptor
                {
                    if (!pendingGce) // 인코더가 GCE를 안 만들었으면 새로 삽입
                    {
                        ushort d = frameIndex < delays.Length ? delays[frameIndex] : (ushort)10;
                        outB.AddRange(new byte[] {
                            0x21, 0xF9, 0x04, 0x00,
                            (byte)(d & 0xFF), (byte)((d >> 8) & 0xFF),
                            0x00, 0x00
                        });
                    }
                    pendingGce = false;
                    frameIndex++;

                    byte ipacked = src[pos + 9];
                    int lctSize = (ipacked & 0x80) != 0 ? 3 * (1 << ((ipacked & 0x07) + 1)) : 0;
                    for (int i = 0; i < 10 + lctSize; i++) outB.Add(src[pos + i]);
                    pos += 10 + lctSize;

                    outB.Add(src[pos]); // LZW minimum code size
                    pos++;
                    pos = CopySubBlocks(src, pos, outB);
                }
                else
                {
                    throw new InvalidDataException("GIF 구조 해석 실패: 0x" + b.ToString("X2") + " @" + pos);
                }
            }
            return outB.ToArray();
        }

        static int CopySubBlocks(byte[] src, int pos, List<byte> outB)
        {
            while (src[pos] != 0)
            {
                int len = src[pos];
                for (int i = 0; i <= len; i++) outB.Add(src[pos + i]);
                pos += len + 1;
            }
            outB.Add(0);
            return pos + 1;
        }
    }

    // ---------------------------------------------------------------
    // Win32
    // ---------------------------------------------------------------
    static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        const int CURSOR_SHOWING = 1;
        const int DI_NORMAL = 3;

        [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CURSORINFO pci);
        [DllImport("user32.dll")] static extern IntPtr CopyIcon(IntPtr hIcon);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("user32.dll")] static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);
        [DllImport("user32.dll")] static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr hIcon,
            int cxWidth, int cyHeight, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // 현재 마우스 커서를 캡처 이미지 위에 그린다.
        public static void DrawCursor(Graphics g, Rectangle region)
        {
            var ci = new CURSORINFO();
            ci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
            if (!GetCursorInfo(ref ci) || ci.flags != CURSOR_SHOWING || ci.hCursor == IntPtr.Zero)
                return;

            IntPtr hIcon = CopyIcon(ci.hCursor);
            if (hIcon == IntPtr.Zero) return;
            try
            {
                int hotX = 0, hotY = 0;
                ICONINFO ii;
                if (GetIconInfo(hIcon, out ii))
                {
                    hotX = ii.xHotspot;
                    hotY = ii.yHotspot;
                    if (ii.hbmMask != IntPtr.Zero) DeleteObject(ii.hbmMask);
                    if (ii.hbmColor != IntPtr.Zero) DeleteObject(ii.hbmColor);
                }
                int x = ci.ptScreenPos.X - hotX - region.X;
                int y = ci.ptScreenPos.Y - hotY - region.Y;
                IntPtr hdc = g.GetHdc();
                try { DrawIconEx(hdc, x, y, hIcon, 0, 0, 0, IntPtr.Zero, DI_NORMAL); }
                finally { g.ReleaseHdc(hdc); }
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
    }

    // ---------------------------------------------------------------
    // --selftest : UI 없이 인코딩 파이프라인을 검증한다.
    // 결과는 exe 옆의 selftest.log에 기록되고 종료 코드로 성공(0)/실패(1)를 반환한다.
    // ---------------------------------------------------------------
    static class SelfTest
    {
        public static int Run()
        {
            var log = new StringBuilder();
            bool pass = true;
            string gifPath = Path.Combine(Path.GetTempPath(), "gifcreator_selftest.gif");

            try
            {
                // 1. 합성 프레임 생성 (움직이는 사각형, 10fps 12프레임)
                var frames = new List<byte[]>();
                var times = new List<int>();
                const int W = 320, H = 180, N = 12, FPS = 10;
                for (int i = 0; i < N; i++)
                {
                    using (var bmp = new Bitmap(W, H, PixelFormat.Format32bppRgb))
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.FromArgb(30, 30, 46));
                        using (var br = new SolidBrush(Color.FromArgb(255, 137, 180, 250)))
                            g.FillRectangle(br, 10 + i * 22, 60, 48, 48);
                        g.DrawString("frame " + i, SystemFonts.DefaultFont, Brushes.White, 8, 8);
                        using (var ms = new MemoryStream())
                        {
                            bmp.Save(ms, ImageFormat.Png);
                            frames.Add(ms.ToArray());
                        }
                    }
                    times.Add(i * 100);
                }

                // 2. 인코딩
                GifWriter.Write(gifPath, frames, times, FPS);
                long size = new FileInfo(gifPath).Length;
                log.AppendLine("encoded: " + gifPath + " (" + size + " bytes)");
                pass &= Check(log, size > 500, "gif file has content");

                // 3. 디코딩 검증
                using (var fs = new FileStream(gifPath, FileMode.Open, FileAccess.Read))
                {
                    var dec = new System.Windows.Media.Imaging.GifBitmapDecoder(
                        fs,
                        System.Windows.Media.Imaging.BitmapCreateOptions.None,
                        System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                    pass &= Check(log, dec.Frames.Count == N, "frame count == " + N + " (got " + dec.Frames.Count + ")");
                    pass &= Check(log, dec.Frames[0].PixelWidth == W && dec.Frames[0].PixelHeight == H,
                        "frame size == " + W + "x" + H);
                    for (int i = 0; i < dec.Frames.Count; i++)
                    {
                        var meta = (System.Windows.Media.Imaging.BitmapMetadata)dec.Frames[i].Metadata;
                        object delay = meta.GetQuery("/grctlext/Delay");
                        bool ok = delay is ushort && (ushort)delay == 10;
                        pass &= Check(log, ok, "frame " + i + " delay == 10cs (got " + delay + ")");
                    }
                }

                // 4. 무한 반복 확장 존재 확인
                var bytes = File.ReadAllBytes(gifPath);
                bool hasLoop = Encoding.ASCII.GetString(bytes).Contains("NETSCAPE2.0");
                pass &= Check(log, hasLoop, "NETSCAPE2.0 loop extension present");

                // 5. 화면 캡처 스모크 테스트 (세션에 데스크톱이 없으면 건너뜀)
                try
                {
                    using (var bmp = new Bitmap(120, 80, PixelFormat.Format32bppRgb))
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, new Size(120, 80));
                        NativeMethods.DrawCursor(g, new Rectangle(0, 0, 120, 80));
                    }
                    log.AppendLine("[ok] screen capture smoke test");
                }
                catch (Exception ex)
                {
                    log.AppendLine("[skip] screen capture unavailable: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                pass = false;
                log.AppendLine("[fail] exception: " + ex);
            }

            log.AppendLine(pass ? "PASS" : "FAIL");
            try
            {
                string logPath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "selftest.log");
                File.WriteAllText(logPath, log.ToString());
            }
            catch (Exception) { }
            return pass ? 0 : 1;
        }

        static bool Check(StringBuilder log, bool cond, string what)
        {
            log.AppendLine((cond ? "[ok] " : "[fail] ") + what);
            return cond;
        }
    }
}
