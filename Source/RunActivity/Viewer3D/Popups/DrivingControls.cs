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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using ORTS.Common;

namespace Orts.Viewer3D.Popups
{
    /// <summary>
    /// Bottom-centre driving deck: interactive throttle & brake sliders, quick-action buttons
    /// (0% idle, 100% full, quick brake release, emergency brake), F/N/R direction buttons,
    /// and arcade/real physics toggle, all synchronized with the locomotive's controllers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The controls call the same controller methods as the keyboard commands, so the simulated
    /// levers - and the cab - follow them. Input is read in <see cref="Prepare"/>, which the
    /// Updater thread runs immediately before <c>Simulator.Update</c> on the same thread, so the
    /// deck never touches the simulation from a different thread than the simulation itself.
    /// </para>
    /// <para>Press <c>U</c> or click the deck close button to hide/show the controls for a clean screen.</para>
    /// </remarks>
    sealed class DrivingControls
    {
        static readonly string[] ReverserLabels = { "F", "N", "R" };
        const float DragResolution = 0.02f;

        readonly WindowManager Owner;
        readonly Viewer Viewer;
        readonly WindowTextFont LabelFont;
        readonly WindowTextFont ValueFont;
        readonly int[] Scratch = new int[64];

        readonly string ThrottleCaption;
        readonly string BrakeCaption;
        readonly string ReverserCaption;
        readonly string ModeCaption;

        public bool Visible = true;
        bool DraggingThrottle;
        bool DraggingBrake;
        float CommandedThrottle = -1;
        float CommandedBrake = -1;

        // Row geometry in pixels; rebuilt only when the display size changes.
        Point LayoutSize = new Point(-1, -1);
        Rectangle Deck;
        Rectangle MiniTogglePill;
        Rectangle CloseBtn;
        Rectangle ThrottleLabel, ThrottleZeroBtn, ThrottleTrack, ThrottleFullBtn, ThrottleValueArea;
        Rectangle BrakeLabel, BrakeRelBtn, BrakeTrack, BrakeEmgBtn, BrakeValueArea;
        readonly Rectangle[] ReverserSegments = new Rectangle[ReverserLabels.Length];
        Rectangle ReverserCaptionArea;
        Rectangle ModePill, ModeCaptionArea;

        // Live state, rebuilt on the HUD's 4Hz refresh.
        string ThrottleText;
        string BrakeText;
        string ModeText;
        float Throttle;
        float Brake;
        bool Arcade;
        int ReverserIndex = 1;
        bool ReverserLocked;

        public DrivingControls(WindowManager owner)
        {
            Owner = owner;
            Viewer = owner.Viewer;
            LabelFont = owner.TextFontSmall;
            ValueFont = owner.TextFontDefaultBold;

            ThrottleCaption = Viewer.Catalog.GetString("THROTTLE");
            BrakeCaption = Viewer.Catalog.GetString("TRAIN BRAKE");
            ReverserCaption = Viewer.Catalog.GetString("DIRECTION");
            ModeCaption = Viewer.Catalog.GetString("MODE");
        }

        /// <summary>Handles pointer interactions, hotkeys, and controller synchronisation.</summary>
        public void Prepare(bool updateFull)
        {
            if (Owner.ScreenSize != LayoutSize)
                Layout();

            if (!UserInput.ComposingMessage && KeyPressed(Keys.U))
                Visible = !Visible;

            if (!Visible)
            {
                if (UserInput.IsMouseLeftButtonPressed && MiniTogglePill.Contains(UserInput.MouseX, UserInput.MouseY))
                    Visible = true;
                return;
            }

            var locomotive = Viewer.PlayerLocomotive as MSTSLocomotive;
            if (locomotive == null)
                return;

            // The bars stay live so they track the simulated levers while those are still moving.
            Throttle = MathHelper.Clamp(locomotive.ThrottlePercent * 0.01f, 0, 1);
            Brake = locomotive.TrainBrakeController != null ? MathHelper.Clamp(locomotive.TrainBrakeController.CurrentValue, 0, 1) : 0;
            ReverserIndex = ReverserIndexOf(locomotive);
            ReverserLocked = locomotive.ThrottlePercent >= 1 || Math.Abs(locomotive.SpeedMpS) > 1 || locomotive.DynamicBrakeController?.CurrentValue > 0;

            HandlePointer(locomotive);

            if (updateFull)
            {
                ThrottleText = ((int)Math.Round(Throttle * 100f)).ToString() + "%";
                BrakeText = ((int)Math.Round(Brake * 100f)).ToString() + "%";
                var arcade = Viewer.Simulator.ArcadeTractiveEffortFactor;
                Arcade = arcade > 1f;
                ModeText = Arcade
                    ? Viewer.Catalog.GetStringFmt("ARCADE {0:F1}x", arcade)
                    : Viewer.Catalog.GetString("REAL PHYSICS");
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            var white = WindowManager.WhiteTexture;

            if (!Visible)
            {
                // Subtle bottom-centre toggle pill when deck is hidden
                spriteBatch.Draw(white, MiniTogglePill, ModernDrivingHUD.PanelColor);
                DrawBorder(spriteBatch, white, MiniTogglePill);
                LabelFont.Draw(spriteBatch, MiniTogglePill, Point.Zero, "▲ CONTROLS (U)", LabelAlignment.Center, ModernDrivingHUD.TextColor, Scratch);
                return;
            }

            if (ThrottleText == null)
                return;

            // Translucent deck container
            spriteBatch.Draw(white, Deck, ModernDrivingHUD.PanelColor);
            DrawBorder(spriteBatch, white, Deck);

            // Captions
            LabelFont.Draw(spriteBatch, ThrottleLabel, Point.Zero, ThrottleCaption, LabelAlignment.Left, ModernDrivingHUD.MutedColor, Scratch);
            LabelFont.Draw(spriteBatch, BrakeLabel, Point.Zero, BrakeCaption, LabelAlignment.Left, ModernDrivingHUD.MutedColor, Scratch);
            LabelFont.Draw(spriteBatch, ReverserCaptionArea, Point.Zero, ReverserCaption, LabelAlignment.Left, ModernDrivingHUD.MutedColor, Scratch);
            LabelFont.Draw(spriteBatch, ModeCaptionArea, Point.Zero, ModeCaption, LabelAlignment.Left, ModernDrivingHUD.MutedColor, Scratch);

            // Close button [ ✕ ]
            DrawButton(spriteBatch, white, CloseBtn, "✕", ModernDrivingHUD.MutedColor, ModernDrivingHUD.TrackColor);

            // Throttle: [ 0% ] [==== Slider ====] [ 100% ] [ Value% ]
            DrawButton(spriteBatch, white, ThrottleZeroBtn, "0%", ModernDrivingHUD.MutedColor, ModernDrivingHUD.TrackColor);
            DrawBar(spriteBatch, white, ThrottleTrack, ThrottleValueArea, Throttle, ThrottleText, ModernDrivingHUD.AccentColor);
            DrawButton(spriteBatch, white, ThrottleFullBtn, "100%", ModernDrivingHUD.MutedColor, ModernDrivingHUD.TrackColor);

            // Brake: [ REL ] [==== Slider ====] [ EMG ] [ Value% ]
            DrawButton(spriteBatch, white, BrakeRelBtn, "REL", ModernDrivingHUD.TextColor, ModernDrivingHUD.TrackColor);
            DrawBar(spriteBatch, white, BrakeTrack, BrakeValueArea, Brake, BrakeText, ModernDrivingHUD.TrainBrakeColor);
            DrawButton(spriteBatch, white, BrakeEmgBtn, "EMG", ModernDrivingHUD.TrainBrakeColor, ModernDrivingHUD.TrackColor);

            // Reverser F / N / R
            for (var i = 0; i < ReverserSegments.Length; i++)
            {
                var active = i == ReverserIndex;
                spriteBatch.Draw(white, ReverserSegments[i], active ? ModernDrivingHUD.AccentColor : ModernDrivingHUD.TrackColor);
                DrawBorder(spriteBatch, white, ReverserSegments[i]);
                ValueFont.Draw(spriteBatch, ReverserSegments[i], Point.Zero, ReverserLabels[i], LabelAlignment.Center,
                    active ? ModernDrivingHUD.TextColor : ReverserLocked ? ModernDrivingHUD.DynamicColor : ModernDrivingHUD.MutedColor, Scratch);
            }

            // Arcade / Pure Physics Mode
            spriteBatch.Draw(white, ModePill, Arcade ? ModernDrivingHUD.AccentColor : ModernDrivingHUD.TrackColor);
            DrawBorder(spriteBatch, white, ModePill);
            ValueFont.Draw(spriteBatch, ModePill, Point.Zero, ModeText, LabelAlignment.Center,
                Arcade ? ModernDrivingHUD.TextColor : ModernDrivingHUD.MutedColor, Scratch);
        }

        static void DrawBorder(SpriteBatch spriteBatch, Texture2D white, Rectangle rect)
        {
            spriteBatch.Draw(white, new Rectangle(rect.X, rect.Y, rect.Width, 1), ModernDrivingHUD.BorderColor);
            spriteBatch.Draw(white, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), ModernDrivingHUD.BorderColor);
            spriteBatch.Draw(white, new Rectangle(rect.X, rect.Y, 1, rect.Height), ModernDrivingHUD.BorderColor);
            spriteBatch.Draw(white, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), ModernDrivingHUD.BorderColor);
        }

        void DrawButton(SpriteBatch spriteBatch, Texture2D white, Rectangle rect, string text, Color textColor, Color bgColor)
        {
            spriteBatch.Draw(white, rect, bgColor);
            DrawBorder(spriteBatch, white, rect);
            ValueFont.Draw(spriteBatch, rect, Point.Zero, text, LabelAlignment.Center, textColor, Scratch);
        }

        void DrawBar(SpriteBatch spriteBatch, Texture2D white, Rectangle track, Rectangle valueArea, float fraction, string text, Color fill)
        {
            spriteBatch.Draw(white, track, ModernDrivingHUD.TrackColor);
            DrawBorder(spriteBatch, white, track);
            var filled = (int)(track.Width * fraction);
            if (filled > 0)
                spriteBatch.Draw(white, new Rectangle(track.X, track.Y, filled, track.Height), fill);
            ValueFont.Draw(spriteBatch, valueArea, Point.Zero, text, LabelAlignment.Right, ModernDrivingHUD.TextColor, Scratch);
        }

        void HandlePointer(MSTSLocomotive locomotive)
        {
            var x = UserInput.MouseX;
            var y = UserInput.MouseY;

            if (UserInput.IsMouseLeftButtonPressed)
            {
                if (CloseBtn.Contains(x, y))
                {
                    Visible = false;
                    return;
                }

                if (ThrottleZeroBtn.Contains(x, y))
                {
                    ForceCommandThrottle(locomotive, 0f, ref CommandedThrottle);
                }
                else if (ThrottleFullBtn.Contains(x, y))
                {
                    ForceCommandThrottle(locomotive, 1f, ref CommandedThrottle);
                }
                else if (ThrottleTrack.Contains(x, y))
                {
                    DraggingThrottle = true;
                    CommandedThrottle = -1;
                    CommandThrottle(locomotive, Fraction(ThrottleTrack, x), ref CommandedThrottle);
                }

                if (BrakeRelBtn.Contains(x, y))
                {
                    ForceCommandBrake(locomotive, 0f, ref CommandedBrake);
                }
                else if (BrakeEmgBtn.Contains(x, y))
                {
                    ForceCommandBrake(locomotive, 1f, ref CommandedBrake);
                }
                else if (BrakeTrack.Contains(x, y))
                {
                    DraggingBrake = true;
                    CommandedBrake = -1;
                    CommandBrake(locomotive, Fraction(BrakeTrack, x), ref CommandedBrake);
                }

                if (ModePill.Contains(x, y))
                    ToggleArcade();

                for (var i = 0; i < ReverserSegments.Length; i++)
                    if (ReverserSegments[i].Contains(x, y))
                    {
                        SetReverser(locomotive, i);
                        break;
                    }
            }

            if (DraggingThrottle)
            {
                if (UserInput.IsMouseLeftButtonDown)
                    CommandThrottle(locomotive, Fraction(ThrottleTrack, x), ref CommandedThrottle);
                else
                    DraggingThrottle = false;
            }

            if (DraggingBrake)
            {
                if (UserInput.IsMouseLeftButtonDown)
                    CommandBrake(locomotive, Fraction(BrakeTrack, x), ref CommandedBrake);
                else
                    DraggingBrake = false;
            }
        }

        static float Fraction(Rectangle track, int x) => MathHelper.Clamp((x - track.X) / (float)track.Width, 0, 1);

        static void CommandThrottle(MSTSLocomotive locomotive, float value, ref float commanded)
        {
            if (Math.Abs(value - commanded) < DragResolution)
                return;
            commanded = value;
            locomotive.ThrottleController?.SetValue(value, true);
        }

        static void ForceCommandThrottle(MSTSLocomotive locomotive, float value, ref float commanded)
        {
            commanded = value;
            locomotive.ThrottleController?.SetValue(value, true);
        }

        static void CommandBrake(MSTSLocomotive locomotive, float value, ref float commanded)
        {
            if (Math.Abs(value - commanded) < DragResolution)
                return;
            commanded = value;
            locomotive.TrainBrakeController?.SetValue(value);
        }

        static void ForceCommandBrake(MSTSLocomotive locomotive, float value, ref float commanded)
        {
            commanded = value;
            locomotive.TrainBrakeController?.SetValue(value);
        }

        void SetReverser(MSTSLocomotive locomotive, int index)
        {
            if (ReverserLocked)
            {
                Viewer.Simulator.Confirmer.Warning(Viewer.Catalog.GetString("Close throttle, release the dynamic brake and stop before changing the reverser."));
                return;
            }

            var guard = 0;
            while (ReverserIndex != index && guard++ < ReverserLabels.Length)
            {
                if (index < ReverserIndex)
                    locomotive.StartReverseIncrease(null);
                else
                    locomotive.StartReverseDecrease(null);
                ReverserIndex = ReverserIndexOf(locomotive);
            }
        }

        static int ReverserIndexOf(MSTSLocomotive locomotive) =>
            locomotive.Direction == Direction.Forward ? 0 : locomotive.Direction == Direction.Reverse ? 2 : 1;

        void ToggleArcade()
        {
            var simulator = Viewer.Simulator;
            simulator.ArcadeTractiveEffortFactor = simulator.ArcadeTractiveEffortFactor > 1f
                ? 1f
                : Simulator.ArcadeTractiveEffortDefault;
        }

        static bool KeyPressed(Keys key)
        {
            var down = Array.IndexOf(UserInput.GetPressedKeys(), key) >= 0;
            return down && Array.IndexOf(UserInput.GetPreviousPressedKeys(), key) < 0;
        }

        void Layout()
        {
            LayoutSize = Owner.ScreenSize;

            var h = ValueFont.Height;
            var gap = h / 2;
            var pad = h / 2;
            var labelHeight = LabelFont.Height;
            var labelWidth = h * 5;
            var quickBtnWidth = (int)(h * 2.2f);
            var relBtnWidth = (int)(h * 2.5f);
            var trackWidth = h * 7;
            var valueWidth = (int)(h * 2.8f);
            var reverserWidth = (int)(h * 1.8f);
            var reverserHeight = h + h / 2;
            var trackHeight = Math.Max(6, h - 4);
            var pillWidth = (int)(h * 8.5f);
            var closeWidth = (int)(h * 1.8f);
            var margin = h * 3 / 2;

            var throttleColWidth = quickBtnWidth + gap + trackWidth + gap + quickBtnWidth + gap + valueWidth;
            var brakeColWidth = relBtnWidth + gap + trackWidth + gap + quickBtnWidth + gap + valueWidth;

            var height = pad * 2 + labelHeight + gap + reverserHeight;
            var width = pad * 2 + throttleColWidth + gap * 2 + brakeColWidth + gap * 2 + (reverserWidth + gap) * ReverserLabels.Length + gap + pillWidth + gap + closeWidth;

            Deck = new Rectangle((Owner.ScreenSize.X - width) / 2, Math.Max(0, Owner.ScreenSize.Y - height - margin), width, height);

            var miniW = (int)(h * 8);
            MiniTogglePill = new Rectangle((Owner.ScreenSize.X - miniW) / 2, Owner.ScreenSize.Y - h - 14, miniW, h + 8);

            var captionY = Deck.Y + pad;
            var rowY = captionY + labelHeight + gap;

            // Throttle column
            var currentX = Deck.X + pad;
            ThrottleLabel = new Rectangle(currentX, captionY, throttleColWidth, labelHeight);
            ThrottleZeroBtn = new Rectangle(currentX, rowY, quickBtnWidth, reverserHeight);
            currentX += quickBtnWidth + gap;
            ThrottleTrack = new Rectangle(currentX, rowY + (reverserHeight - trackHeight) / 2, trackWidth, trackHeight);
            currentX += trackWidth + gap;
            ThrottleFullBtn = new Rectangle(currentX, rowY, quickBtnWidth, reverserHeight);
            currentX += quickBtnWidth + gap;
            ThrottleValueArea = new Rectangle(currentX, rowY + (reverserHeight - h) / 2, valueWidth, h);
            currentX += valueWidth + gap * 2;

            // Brake column
            BrakeLabel = new Rectangle(currentX, captionY, brakeColWidth, labelHeight);
            BrakeRelBtn = new Rectangle(currentX, rowY, relBtnWidth, reverserHeight);
            currentX += relBtnWidth + gap;
            BrakeTrack = new Rectangle(currentX, rowY + (reverserHeight - trackHeight) / 2, trackWidth, trackHeight);
            currentX += trackWidth + gap;
            BrakeEmgBtn = new Rectangle(currentX, rowY, quickBtnWidth, reverserHeight);
            currentX += quickBtnWidth + gap;
            BrakeValueArea = new Rectangle(currentX, rowY + (reverserHeight - h) / 2, valueWidth, h);
            currentX += valueWidth + gap * 2;

            // Direction column
            ReverserCaptionArea = new Rectangle(currentX, captionY, (reverserWidth + gap) * ReverserLabels.Length, labelHeight);
            for (var i = 0; i < ReverserSegments.Length; i++)
            {
                ReverserSegments[i] = new Rectangle(currentX + i * (reverserWidth + gap), rowY, reverserWidth, reverserHeight);
            }
            currentX += (reverserWidth + gap) * ReverserLabels.Length + gap;

            // Mode column
            ModeCaptionArea = new Rectangle(currentX, captionY, pillWidth, labelHeight);
            ModePill = new Rectangle(currentX, rowY, pillWidth, reverserHeight);
            currentX += pillWidth + gap;

            // Close button [ ✕ ]
            CloseBtn = new Rectangle(currentX, rowY, closeWidth, reverserHeight);
        }
    }
}
