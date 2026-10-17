// COPYRIGHT 2026 by the Open Rails project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team.

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using ORTS.Common;

namespace Orts.Viewer3D.Popups
{
    /// <summary>
    /// Minimalist digital driving overlay: a large speed readout, the control meters, the
    /// reverser position and the speed-limit / signal-aspect badges, on a translucent panel.
    /// </summary>
    /// <remarks>
    /// Every string and rectangle is prepared on the Updater thread by <see cref="Prepare"/>,
    /// so <see cref="Draw"/> only submits pre-built quads and reuses buffers it already owns.
    /// </remarks>
    sealed class ModernDrivingHUD
    {
        // AMOLED palette, matching the launcher.
        static readonly Color PanelColor = new Color(0x00, 0x00, 0x00, 0x99); // #000000 at 60%
        static readonly Color BorderColor = new Color(0x22, 0x22, 0x22);
        static readonly Color TrackColor = new Color(0x1C, 0x1C, 0x1C);
        static readonly Color TextColor = new Color(0xFF, 0xFF, 0xFF);
        static readonly Color MutedColor = new Color(0x8C, 0x8C, 0x8C);
        static readonly Color AccentColor = new Color(0x25, 0x63, 0xEB);
        static readonly Color DynamicColor = new Color(0xE0, 0x9B, 0x2B);
        static readonly Color TrainBrakeColor = new Color(0xE0, 0x3B, 0x3B);
        static readonly Color IndependentColor = new Color(0xB4, 0xB4, 0xB4);

        const int ThrottleMeter = 0;
        const int DynamicMeter = 1;
        const int TrainBrakeMeter = 2;
        const int IndependentMeter = 3;
        const int MeterCount = 4;

        static readonly string[] ReverserLabels = { "F", "N", "R" };

        // Signal aspects are 16x16 sprites in a 2 column x 5 row grid in SignalAspects.png.
        static Texture2D SignalAspects;
        static readonly Dictionary<TrackMonitorSignalAspect, Rectangle> SignalSprites = new Dictionary<TrackMonitorSignalAspect, Rectangle>
        {
            { TrackMonitorSignalAspect.Clear_2, new Rectangle(0, 0, 16, 16) },
            { TrackMonitorSignalAspect.Clear_1, new Rectangle(16, 0, 16, 16) },
            { TrackMonitorSignalAspect.Approach_3, new Rectangle(0, 16, 16, 16) },
            { TrackMonitorSignalAspect.Approach_2, new Rectangle(16, 16, 16, 16) },
            { TrackMonitorSignalAspect.Approach_1, new Rectangle(0, 32, 16, 16) },
            { TrackMonitorSignalAspect.Restricted, new Rectangle(16, 32, 16, 16) },
            { TrackMonitorSignalAspect.StopAndProceed, new Rectangle(0, 48, 16, 16) },
            { TrackMonitorSignalAspect.Stop, new Rectangle(16, 48, 16, 16) },
            { TrackMonitorSignalAspect.Permission, new Rectangle(0, 64, 16, 16) },
            { TrackMonitorSignalAspect.None, new Rectangle(16, 64, 16, 16) },
        };

        readonly WindowManager Owner;
        readonly Viewer Viewer;
        readonly WindowTextFont DigitFont;
        readonly WindowTextFont LabelFont;
        readonly WindowTextFont ValueFont;

        // Buffers reused for the whole lifetime of the overlay.
        readonly int[] Scratch = new int[24];
        readonly float[] MeterValues = new float[MeterCount];
        readonly string[] MeterTexts = new string[MeterCount];
        readonly string[] MeterLabels = new string[MeterCount];
        readonly Color[] MeterColors = new Color[MeterCount];

        string SpeedText;
        string SpeedUnit;
        string LimitCaption;
        string LimitText;
        bool HasSignal;
        Rectangle SignalSprite;
        int ReverserIndex = 1;

        // Row sizes in pixels; rebuilt only when the display size changes.
        Point LayoutSize = new Point(-1, -1);
        Rectangle Panel;
        Rectangle SpeedArea;
        Rectangle UnitArea;
        Rectangle LimitCaptionArea;
        Rectangle LimitArea;
        Rectangle SignalBadge;
        int MeterX, MeterY, MeterRowHeight, LabelWidth, BarWidth, ValueWidth, Gap, BarHeight;
        int ReverserX, ReverserY, ReverserSegmentWidth, ReverserHeight;

        public ModernDrivingHUD(WindowManager owner)
        {
            Owner = owner;
            Viewer = owner.Viewer;
            DigitFont = owner.TextFontLargeBold;
            LabelFont = owner.TextFontSmall;
            ValueFont = owner.TextFontDefaultBold;

            MeterLabels[ThrottleMeter] = Viewer.Catalog.GetString("THROTTLE");
            MeterLabels[DynamicMeter] = Viewer.Catalog.GetString("DYNAMIC BRAKE");
            MeterLabels[TrainBrakeMeter] = Viewer.Catalog.GetString("TRAIN BRAKE");
            MeterLabels[IndependentMeter] = Viewer.Catalog.GetString("INDEPENDENT");
            MeterColors[ThrottleMeter] = AccentColor;
            MeterColors[DynamicMeter] = DynamicColor;
            MeterColors[TrainBrakeMeter] = TrainBrakeColor;
            MeterColors[IndependentMeter] = IndependentColor;
            LimitCaption = Viewer.Catalog.GetString("LIMIT");

            if (SignalAspects == null)
                // TODO: This should happen on the loader thread, as the Track Monitor's own copy should.
                SignalAspects = SharedTextureManager.LoadInternal(Viewer.RenderProcess.GraphicsDevice, System.IO.Path.Combine(Viewer.ContentPath, "SignalAspects.png"));
        }

        /// <summary>Reads the simulator, and rebuilds the strings and geometry <see cref="Draw"/> will use.</summary>
        public void Prepare(bool updateFull)
        {
            var locomotive = Viewer.PlayerLocomotive;
            if (locomotive == null)
                return;

            if (Owner.ScreenSize != LayoutSize)
                Layout();

            var msts = locomotive as MSTSLocomotive;

            MeterValues[ThrottleMeter] = locomotive.ThrottlePercent * 0.01f;
            MeterValues[DynamicMeter] = msts != null ? msts.DynamicBrakePercent * 0.01f : 0f;
            MeterValues[TrainBrakeMeter] = msts?.TrainBrakeController != null ? msts.TrainBrakeController.IntermediateValue : 0f;
            MeterValues[IndependentMeter] = msts?.EngineBrakeController != null ? msts.EngineBrakeController.IntermediateValue : 0f;
            for (var i = 0; i < MeterCount; i++)
                MeterValues[i] = MathHelper.Clamp(MeterValues[i], 0f, 1f);

            ReverserIndex = locomotive.Direction == Direction.Forward ? 0 : locomotive.Direction == Direction.Reverse ? 2 : 1;

            // The speed is the one reading that has to stay live, so it is rebuilt every frame.
            SpeedText = FormatStrings.FormatSpeedLimitNoUoM(Math.Abs(locomotive.SpeedMpS), locomotive.IsMetric);

            if (updateFull)
            {
                SpeedUnit = locomotive.IsMetric ? FormatStrings.kmph : FormatStrings.mph;
                for (var i = 0; i < MeterCount; i++)
                    MeterTexts[i] = ((int)Math.Round(MeterValues[i] * 100f)).ToString() + "%";

                // Speed limit and next signal come from the same train info the Track Monitor uses.
                var info = Viewer.PlayerTrain.GetTrainInfo();
                LimitText = FormatStrings.FormatSpeedLimitNoUoM(info.allowedSpeedMpS, locomotive.IsMetric);
                HasSignal = FindSignalAspect(info, out SignalSprite);
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (SpeedText == null)
                return;

            var white = WindowManager.WhiteTexture;

            // Translucent panel with a hairline border.
            spriteBatch.Draw(white, Panel, PanelColor);
            spriteBatch.Draw(white, new Rectangle(Panel.X, Panel.Y, Panel.Width, 1), BorderColor);
            spriteBatch.Draw(white, new Rectangle(Panel.X, Panel.Bottom - 1, Panel.Width, 1), BorderColor);
            spriteBatch.Draw(white, new Rectangle(Panel.X, Panel.Y, 1, Panel.Height), BorderColor);
            spriteBatch.Draw(white, new Rectangle(Panel.Right - 1, Panel.Y, 1, Panel.Height), BorderColor);

            DigitFont.Draw(spriteBatch, SpeedArea, Point.Zero, SpeedText, LabelAlignment.Right, TextColor, Scratch);
            LabelFont.Draw(spriteBatch, UnitArea, Point.Zero, SpeedUnit, LabelAlignment.Left, MutedColor, Scratch);
            LabelFont.Draw(spriteBatch, LimitCaptionArea, Point.Zero, LimitCaption, LabelAlignment.Left, MutedColor, Scratch);
            ValueFont.Draw(spriteBatch, LimitArea, Point.Zero, LimitText, LabelAlignment.Right, TextColor, Scratch);

            if (HasSignal)
                spriteBatch.Draw(SignalAspects, SignalBadge, SignalSprite, Color.White);

            for (var i = 0; i < MeterCount; i++)
            {
                var row = MeterY + i * MeterRowHeight;

                LabelFont.Draw(spriteBatch, new Rectangle(MeterX, row + (MeterRowHeight - LabelFont.Height) / 2, LabelWidth, LabelFont.Height), Point.Zero, MeterLabels[i], LabelAlignment.Left, MutedColor, Scratch);

                var bar = new Rectangle(MeterX + LabelWidth + Gap, row + (MeterRowHeight - BarHeight) / 2, BarWidth, BarHeight);
                spriteBatch.Draw(white, bar, TrackColor);
                var filled = (int)(BarWidth * MeterValues[i]);
                if (filled > 0)
                    spriteBatch.Draw(white, new Rectangle(bar.X, bar.Y, filled, bar.Height), MeterColors[i]);

                ValueFont.Draw(spriteBatch, new Rectangle(bar.Right + Gap, row + (MeterRowHeight - ValueFont.Height) / 2, ValueWidth, ValueFont.Height), Point.Zero, MeterTexts[i], LabelAlignment.Right, TextColor, Scratch);
            }

            for (var i = 0; i < ReverserLabels.Length; i++)
            {
                var segment = new Rectangle(ReverserX + i * (ReverserSegmentWidth + Gap), ReverserY, ReverserSegmentWidth, ReverserHeight);
                var active = i == ReverserIndex;
                spriteBatch.Draw(white, segment, active ? AccentColor : TrackColor);
                ValueFont.Draw(spriteBatch, segment, Point.Zero, ReverserLabels[i], LabelAlignment.Center, active ? TextColor : MutedColor, Scratch);
            }
        }

        /// <summary>Sizes the panel from the scaled font heights, and parks it in the bottom-left corner.</summary>
        void Layout()
        {
            LayoutSize = Owner.ScreenSize;

            var h = ValueFont.Height;
            Gap = h / 2;
            BarHeight = Math.Max(6, h - 6);
            LabelWidth = h * 6;
            BarWidth = h * 12;
            ValueWidth = h * 3;
            MeterRowHeight = h + h / 4;
            ReverserSegmentWidth = h * 2;
            ReverserHeight = h + h / 2;

            var pad = h / 2;
            var speedHeight = DigitFont.Height;
            var signalSize = h + h / 2;

            var width = pad * 2 + LabelWidth + Gap + BarWidth + Gap + ValueWidth;
            var height = pad * 2 + speedHeight + Gap + MeterCount * MeterRowHeight + Gap + ReverserHeight;
            var margin = h * 3 / 2;

            Panel = new Rectangle(margin, Math.Max(0, Owner.ScreenSize.Y - height - margin), width, height);
            SpeedArea = new Rectangle(Panel.X + pad, Panel.Y + pad, h * 5, speedHeight);
            UnitArea = new Rectangle(SpeedArea.Right + Gap, Panel.Y + pad + speedHeight - LabelFont.Height, h * 3, LabelFont.Height);
            SignalBadge = new Rectangle(Panel.Right - pad - signalSize, Panel.Y + pad + (speedHeight - signalSize) / 2, signalSize, signalSize);
            LimitCaptionArea = new Rectangle(UnitArea.Right + h, Panel.Y + pad + speedHeight - LabelFont.Height, h * 3, LabelFont.Height);
            LimitArea = new Rectangle(LimitCaptionArea.Right, Panel.Y + pad + speedHeight - ValueFont.Height, Math.Max(0, SignalBadge.X - Gap - LimitCaptionArea.Right), ValueFont.Height);

            MeterX = Panel.X + pad;
            MeterY = Panel.Y + pad + speedHeight + Gap;
            ReverserX = Panel.X + pad;
            ReverserY = MeterY + MeterCount * MeterRowHeight + Gap;
        }

        /// <summary>Finds the next signal ahead of the train, and the sprite for its aspect.</summary>
        static bool FindSignalAspect(Train.TrainInfo info, out Rectangle sprite)
        {
            for (var i = 0; i < info.ObjectInfoForward.Count; i++)
            {
                var item = info.ObjectInfoForward[i];
                if (item.ItemType == Train.TrainObjectItem.TRAINOBJECTTYPE.SIGNAL || item.ItemType == Train.TrainObjectItem.TRAINOBJECTTYPE.SPEED_SIGNAL)
                    return SignalSprites.TryGetValue(item.SignalState, out sprite);
            }
            sprite = Rectangle.Empty;
            return false;
        }
    }
}
