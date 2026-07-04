using System;
using System.Diagnostics;
using System.Globalization;

namespace FamiStudio
{
    public class Oscilloscope : Control
    {
        private bool lastOscilloscopeHadNonZeroSample;

        public bool LastOscilloscopeHadNonZeroSample => lastOscilloscopeHadNonZeroSample;

        public Oscilloscope()
        {
        }

        protected override void OnRender(Graphics g)
        {
            var c = g.DefaultCommandList;

            var sx = width;
            var sy = height;

            c.PushClipRegion(1, 1, sx - 1, sy - 1);
            c.FillRectangle(0, 0, sx, sy, Theme.BlackColor);

            bool[] hasNonZeroSamples;
            var oscilloscopeGeometries = App.GetOscilloscopeGeometries(out hasNonZeroSamples);
            bool anyNonZero = false;

            if (oscilloscopeGeometries != null)
            {
                int numChannels = Math.Min(5, oscilloscopeGeometries.Length);
                float channelWidth = sx / (float)numChannels;
                float channelHeight = sy;
                
                for (int i = 0; i < numChannels; i++)
                {
                    var geometry = oscilloscopeGeometries[i];
                    var hasNonZero = hasNonZeroSamples != null && i < hasNonZeroSamples.Length ? hasNonZeroSamples[i] : false;
                    anyNonZero |= hasNonZero;

                    float xOffset = i * channelWidth;
                    float yOffset = sy / 2.0f;

                    if (geometry != null && hasNonZero)
                    {
                        float scaleX = channelWidth;
                        // Exaggerate amplitude by 2.5x for better visibility in the small preview
                        float scaleY = (channelHeight / -2.0f) * 2.5f; 

                        c.PushTransform(xOffset, yOffset, scaleX, scaleY);
                        c.DrawNiceSmoothLine(geometry, Theme.LightGreyColor2);
                        c.PopTransform();
                    }
                    else
                    {
                        c.PushTranslation(xOffset, yOffset);
                        c.DrawLine(0, 0, channelWidth, 0, Theme.LightGreyColor2);
                        c.PopTransform();
                    }
                    
                    if (i > 0)
                    {
                        c.DrawLine(xOffset, 0, xOffset, sy, Theme.DarkGreyColor4);
                    }
                }
            }
            else
            {
                c.PushTranslation(0, sy / 2);
                c.DrawLine(0, 0, sx, 0, Theme.LightGreyColor2);
                c.PopTransform();
            }

            lastOscilloscopeHadNonZeroSample = anyNonZero;

            if (Platform.IsMobile)
            {
                Utils.SplitVersionNumber(Platform.ApplicationVersion, out var betaNumber);

                if (betaNumber > 0)
                    c.DrawText($"BETA {betaNumber}", Fonts.FontSmall, 4, 4, Theme.LightRedColor);
            }

            c.PopClipRegion();
            c.DrawRectangle(0, 0, sx, sy, Theme.LightGreyColor2);
        }

        protected override void OnPointerUp(PointerEventArgs e)
        {
            if (e.Right && Platform.IsDesktop)
            {
                var dialog = new OscilloscopeFullscreenDialog(App);
                dialog.ShowDialogAsync((r) => { });
            }
            base.OnPointerUp(e);
        }
    }
}
