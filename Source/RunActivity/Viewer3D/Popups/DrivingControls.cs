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
    /// Bottom-centre driving deck: a throttle bar and a train-brake bar you can drag, the F/N/R
    /// reverser selector and the arcade pill, all driving the player locomotive's own controllers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The controls call the same controller methods as the keyboard commands, so the simulated
    /// levers - and the cab - follow them. Input is read in <see cref="Prepare"/>, which the
    /// Updater thread runs immediately before <c>Simulator.Update</c> on the same thread, so the
    /// deck never touches the simulation from a different thread than the simulation itself.
    /// </para>
    /// <para>Press <c>U</c> to hide the deck for a clean screen.</para>
    /// </remarks>
    sealed class DrivingControls
    {
        static readonly string[] ReverserLabels = { "F", "N", "R" };
        // Below this the bar is not worth another controller command while dragging.
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
        Rectangle ThrottleLabel, ThrottleTrack, ThrottleValueArea;
        Rectangle BrakeLabel, BrakeTrack, BrakeValueArea;
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

        /// <summary>Handles the toggle key and the pointer, and refreshes the displayed values.</summary>
        public void Prepare(bool updateFull)
        {
            if (!UserInput.ComposingMessage && KeyPressed(Keys.U))
                Visible = !Visible;
            if (!Visible)
                return;

            if (Owner.ScreenSize != LayoutSize)
                Layout();

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
            if (!Visible || ThrottleText == null)
                return;

            var white = WindowManager.WhiteTexture;

            spriteBatch.Draw(white, Deck, ModernDrivingHUD.PanelColor);
            spriteBatch.Draw(white, new Rectangle(Deck.X, Deck.Y, Deck.Width, 1), ModernDrivingHUD.BorderColor);
            spriteBatch.Draw(white, new Rectangle(Deck.X, Deck.Bottom - 1, Deck.Width, 1), ModernDrivingHUD.BorderColor);
            spriteBatch.Draw(white, new Rectangle(Deck.X, Deck.Y, 1, Deck.Height), ModernDrivingHUD.BorderColor);
            spriteBatch.Draw(white, new Rectangle(Deck.Right - 1, Deck.Y, 1, Deck.Height), ModernDrivingHUD.BorderColor);

            LabelFont.Draw(spriteBatch, ThrottleLabel, Point.Zero, ThrottleCaption, LabelAlignment.Left, ModernDrivingHUD.MutedColor, Scratch);
            LabelFont.Draw(spriteBatch, BrakeLabel, Point.Zero, BrakeCaption, LabelAlignment.Left, ModernDrivingHUD.MutedColor, Scratch);
            LabelFont.Draw(spriteBatch, ReverserCaptionArea, Point.Zero, ReverserCaption, LabelAlignment.Left, ModernDrivingHUD.MutedColor, Scratch);
            LabelFont.Draw(spriteBatch, ModeCaptionArea, Point.Zero, ModeCaption, LabelAlignment.Left, ModernDrivingHUD.MutedColor, Scratch);

            DrawBar(spriteBatch, white, ThrottleTrack, ThrottleValueArea, Throttle, ThrottleText, ModernDrivingHUD.AccentColor);
            DrawBar(spriteBatch, white, BrakeTrack, BrakeValueArea, Brake, BrakeText, ModernDrivingHUD.TrainBrakeColor);

            for (var i = 0; i < ReverserSegments.Length; i++)
            {
                var active = i == ReverserIndex;
                spriteBatch.Draw(white, ReverserSegments[i], active ? ModernDrivingHUD.AccentColor : ModernDrivingHUD.TrackColor);
                ValueFont.Draw(spriteBatch, ReverserSegments[i], Point.Zero, ReverserLabels[i], LabelAlignment.Center,
                    active ? ModernDrivingHUD.TextColor : ReverserLocked ? ModernDrivingHUD.DynamicColor : ModernDrivingHUD.MutedColor, Scratch);
            }

            spriteBatch.Draw(white, ModePill, Arcade ? ModernDrivingHUD.AccentColor : ModernDrivingHUD.TrackColor);
            ValueFont.Draw(spriteBatch, ModePill, Point.Zero, ModeText, LabelAlignment.Center,
                Arcade ? ModernDrivingHUD.TextColor : ModernDrivingHUD.MutedColor, Scratch);
        }

        void DrawBar(SpriteBatch spriteBatch, Texture2D white, Rectangle track, Rectangle valueArea, float fraction, string text, Color fill)
        {
            spriteBatch.Draw(white, track, ModernDrivingHUD.TrackColor);
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
                if (ThrottleTrack.Contains(x, y))
                {
                    DraggingThrottle = true;
                    CommandedThrottle = -1;
                    CommandThrottle(locomotive, Fraction(ThrottleTrack, x), ref CommandedThrottle);
                }
                if (BrakeTrack.Contains(x, y))
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

        // SetValue moves the lever in one go, which is what dragging a slider means; the controller's
        // own notch layout still decides where it finally lands.
        static void CommandThrottle(MSTSLocomotive locomotive, float value, ref float commanded)
        {
            if (Math.Abs(value - commanded) < DragResolution)
                return;
            commanded = value;
            locomotive.ThrottleController.SetValue(value, true);
        }

        static void CommandBrake(MSTSLocomotive locomotive, float value, ref float commanded)
        {
            if (Math.Abs(value - commanded) < DragResolution)
                return;
            commanded = value;
            locomotive.TrainBrakeController.SetValue(value);
        }

        void SetReverser(MSTSLocomotive locomotive, int index)
        {
            // Same guard as the keyboard command: no reverser changes with the throttle on, the train
            // moving or the dynamic brake applied. The deck reports it by tinting the selector amber.
            if (ReverserLocked)
            {
                Viewer.Simulator.Confirmer.Warning(Viewer.Catalog.GetString("Close throttle, release the dynamic brake and stop before changing the reverser."));
                return;
            }

            // SetDirection takes effect inside the call, so the loop below always terminates - the
            // guard only bounds it in case a locomotive refuses the change for another reason.
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
            var labelWidth = h * 6;
            var trackWidth = h * 10;
            var valueWidth = h * 3;
            var columnWidth = labelWidth + gap + trackWidth + gap + valueWidth;
            var reverserWidth = h * 2;
            var reverserHeight = h + h / 2;
            var trackHeight = Math.Max(6, h - 6);
            var pillWidth = h * 9;
            var margin = h * 3 / 2;

            var height = pad * 2 + labelHeight + gap + reverserHeight;
            var width = pad * 2 + columnWidth * 2 + gap + (reverserWidth + gap) * ReverserLabels.Length + gap + pillWidth;

            Deck = new Rectangle((Owner.ScreenSize.X - width) / 2, Math.Max(0, Owner.ScreenSize.Y - height - margin), width, height);

            var captionY = Deck.Y + pad;
            var rowY = captionY + labelHeight + gap;

            ThrottleLabel = new Rectangle(Deck.X + pad, captionY, labelWidth, labelHeight);
            ThrottleTrack = new Rectangle(ThrottleLabel.Right + gap, rowY + (reverserHeight - trackHeight) / 2, trackWidth, trackHeight);
            ThrottleValueArea = new Rectangle(ThrottleTrack.Right + gap, rowY + (reverserHeight - h) / 2, valueWidth, h);

            BrakeLabel = new Rectangle(ThrottleValueArea.Right + gap, captionY, labelWidth, labelHeight);
            BrakeTrack = new Rectangle(BrakeLabel.Right + gap, ThrottleTrack.Y, trackWidth, trackHeight);
            BrakeValueArea = new Rectangle(BrakeTrack.Right + gap, ThrottleValueArea.Y, valueWidth, h);

            var reverserX = BrakeValueArea.Right + gap;
            for (var i = 0; i < ReverserSegments.Length; i++)
                ReverserSegments[i] = new Rectangle(reverserX + i * (reverserWidth + gap), rowY, reverserWidth, reverserHeight);

            ModePill = new Rectangle(ReverserSegments[ReverserSegments.Length - 1].Right + gap, rowY, pillWidth, reverserHeight);

            ReverserCaptionArea = new Rectangle(ReverserSegments[0].X, captionY, labelWidth, labelHeight);
            ModeCaptionArea = new Rectangle(ModePill.X, captionY, pillWidth, labelHeight);
        }
    }
}
