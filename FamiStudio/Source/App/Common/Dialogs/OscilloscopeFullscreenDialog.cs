using System;

namespace FamiStudio
{
    class OscilloscopeFullscreenDialog : Dialog
    {
        private FamiStudio app;
        
        public OscilloscopeFullscreenDialog(FamiStudio app) : base(app.Window, "")
        {
            this.app = app;
            SetTickEnabled(true);
        }

        public override void Tick(float delta)
        {
            MarkDirty();
            base.Tick(delta);
        }

        protected override void OnPointerDown(PointerEventArgs e)
        {
            if (e.Right)
            {
                Close(DialogResult.OK);
            }
            base.OnPointerDown(e);
        }

        protected override void OnRender(Graphics g)
        {
            var c = g.DefaultCommandList;
            
            // Try to make it cover the entire window
            if (width != FamiStudioWindow.Instance.Width || height != FamiStudioWindow.Instance.Height)
            {
                width = FamiStudioWindow.Instance.Width;
                height = FamiStudioWindow.Instance.Height;
                Move(-left, -top);
            }

            var sx = width;
            var sy = height;

            c.FillRectangle(0, 0, sx, sy, Theme.BlackColor);

            bool[] hasNonZeroSamples;
            var oscilloscopeGeometries = App.GetOscilloscopeGeometries(out hasNonZeroSamples);

            if (oscilloscopeGeometries != null)
            {
                int numChannels = Math.Min(4, oscilloscopeGeometries.Length);
                float channelWidth = sx / 2.0f;
                float channelHeight = sy / 2.0f;
                
                for (int i = 0; i < numChannels; i++)
                {
                    var geometry = oscilloscopeGeometries[i];
                    var hasNonZero = hasNonZeroSamples != null && i < hasNonZeroSamples.Length ? hasNonZeroSamples[i] : false;

                    int col = i % 2;
                    int row = i / 2;

                    float xOffset = col * channelWidth;
                    float yOffset = row * channelHeight + channelHeight / 2.0f;

                    if (geometry != null && hasNonZero)
                    {
                        float scaleX = channelWidth;
                        float scaleY = channelHeight / -2.0f; 

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
                }
            }

            // Draw a tiny instruction for closing
            c.DrawText("Right click to close", Fonts.FontSmall, 4, 4, Theme.LightGreyColor1);
        }
    }
}
