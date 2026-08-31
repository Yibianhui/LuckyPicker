// ================================================================
// FloatingBall.cs — 桌面悬浮球
//
// 一个始终置顶的小圆球：
//   · 单击     → 立即抽一人（滚动动画与语音播报与主界面一致）
//   · 拖动     → 移动位置（自动记忆，重启后保持）
//   · 右键     → 菜单：显示主窗口 / 抽一人 / 连抽五人 / 重置候选池 /
//                开机自启动开关 / 隐藏悬浮球 / 退出程序
//   · 抽中后   → 球面短暂显示抽中姓名，便于投屏时远距离观看
// 隐藏 / 显示可在「版本与更新 → 偏好设置」中切换，位置与状态均持久化。
// ================================================================
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LuckyPicker
{
    public class FloatingBallForm : Form
    {
        const int SizePx = 92;

        readonly MainForm owner;
        bool dragging;
        Point dragStart;
        Point formStart;
        bool movedFar;

        // 抽中姓名短暂显示
        string flashName;
        Timer flashTimer;
        int flashTicks;

        public FloatingBallForm(MainForm owner)
        {
            this.owner = owner;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(SizePx, SizePx);
            DoubleBuffered = true;
            Text = "YBH幸运摇人器 · 悬浮球";

            Region = MakeCircleRegion();

            RestorePosition();

            flashTimer = new Timer { Interval = 120 };
            flashTimer.Tick += delegate
            {
                flashTicks++;
                if (flashTicks >= 25)   // 约 3 秒
                {
                    flashTimer.Stop();
                    flashName = null;
                }
                Invalidate();
            };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("显示主窗口", null, delegate { owner.ShowMainFromBall(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("抽一人", null, delegate { owner.PickOneFromBall(); });
            menu.Items.Add("连抽五人", null, delegate { owner.PickMultiFromBall(); });
            menu.Items.Add("重置候选池", null, delegate { owner.ResetPoolFromBall(); });
            menu.Items.Add(new ToolStripSeparator());
            var bootItem = new ToolStripMenuItem("开机自启动")
            {
                CheckOnClick = true,
                Checked = AutoStart.IsEnabled()
            };
            bootItem.Click += delegate
            {
                bool ok = AutoStart.SetEnabled(bootItem.Checked);
                bootItem.Checked = ok && bootItem.Checked;
                if (!ok)
                    MessageBox.Show("设置开机自启动失败（注册表写入被拒绝）。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
            menu.Items.Add(bootItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("隐藏悬浮球", null, delegate
            {
                AppConfig.BallVisible = false;
                AppConfig.Save();
                owner.ApplyBallVisibility();
            });
            menu.Items.Add("退出程序", null, delegate
            {
                owner.Close();   // 主窗口关闭时一并退出
            });
            ContextMenuStrip = menu;
        }

        Region MakeCircleRegion()
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(0, 0, SizePx, SizePx);
                return new Region(path);
            }
        }

        // ---------- 位置记忆 ----------
        void RestorePosition()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            int x = AppConfig.BallX, y = AppConfig.BallY;
            if (x <= -SizePx || x >= wa.Right || y <= -SizePx || y >= wa.Bottom)
            {
                // 默认：工作区右侧偏上
                x = wa.Right - SizePx - 24;
                y = wa.Top + wa.Height / 3;
            }
            Location = new Point(x, y);
        }

        void SavePosition()
        {
            AppConfig.BallX = Location.X;
            AppConfig.BallY = Location.Y;
            AppConfig.Save();
        }

        // ---------- 抽中闪烁 ----------
        public void ShowPicked(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            flashName = text.Length > 6 ? text.Substring(0, 5) + "…" : text;
            flashTicks = 0;
            flashTimer.Start();
            Invalidate();
        }

        // ---------- 交互 ----------
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                movedFar = false;
                dragStart = Cursor.Position;
                formStart = Location;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            var d = Cursor.Position;
            int dx = d.X - dragStart.X, dy = d.Y - dragStart.Y;
            if (Math.Abs(dx) + Math.Abs(dy) > 5) movedFar = true;
            if (movedFar)
            {
                var wa = Screen.PrimaryScreen.WorkingArea;
                int nx = Math.Max(wa.Left - SizePx / 2, Math.Min(formStart.X + dx, wa.Right - SizePx / 2));
                int ny = Math.Max(wa.Top, Math.Min(formStart.Y + dy, wa.Bottom - SizePx / 2));
                Location = new Point(nx, ny);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            bool wasDrag = dragging && movedFar;
            dragging = false;
            if (wasDrag) { SavePosition(); return; }
            owner.PickOneFromBall();   // 单击 = 抽一人
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Cursor = Cursors.Hand;
            Invalidate();
        }

        // ---------- 绘制 ----------
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(2, 2, SizePx - 5, SizePx - 5);
            bool flash = !string.IsNullOrEmpty(flashName);

            // 阴影 + 渐变球体
            using (var shadow = new GraphicsPath())
            {
                shadow.AddEllipse(4, 6, SizePx - 6, SizePx - 6);
                using (var sb = new PathGradientBrush(shadow))
                {
                    sb.CenterColor = Color.FromArgb(70, 30, 41, 59);
                    sb.SurroundColors = new[] { Color.FromArgb(0, 30, 41, 59) };
                    g.FillPath(sb, shadow);
                }
            }
            using (var gp = new GraphicsPath())
            {
                gp.AddEllipse(rect);
                using (var pgb = new PathGradientBrush(gp))
                {
                    pgb.CenterPoint = new Point(rect.X + rect.Width / 3, rect.Y + rect.Height / 4);
                    pgb.CenterColor = Color.FromArgb(96, 165, 250);
                    pgb.SurroundColors = new[] { Color.FromArgb(37, 99, 235) };
                    g.FillPath(pgb, gp);
                }
            }
            using (var pen = new Pen(Color.White, flash ? 3f : 2f))
                g.DrawEllipse(pen, rect);

            // 球面文字：抽中姓名 / 默认“摇”
            var face = new Rectangle(rect.X, rect.Y + rect.Height / 7, rect.Width, rect.Height);
            if (flash)
            {
                using (var f = new Font("Microsoft YaHei", 12.5F, FontStyle.Bold))
                using (var fmt = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                })
                    g.DrawString(flashName, f, Brushes.White, face, fmt);
            }
            else
            {
                using (var f = new Font("Microsoft YaHei", 20F, FontStyle.Bold))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("摇", f, Brushes.White, face, fmt);
            }
        }
    }
}
