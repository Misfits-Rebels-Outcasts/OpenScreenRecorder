using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Text;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.UI;
using Windows.UI.Text;

namespace VideoBasicEffects
{
    public sealed class BorderVideoEffect : IBasicVideoEffect
    {

        public bool IsReadOnly { get { return false; } }

        public IReadOnlyList<VideoEncodingProperties> SupportedEncodingProperties { get { return new List<VideoEncodingProperties>(); } }

        public MediaMemoryTypes SupportedMemoryTypes { get { return MediaMemoryTypes.Gpu; } }

        public bool TimeIndependent { get { return true; } }

        public void Close(MediaEffectClosedReason reason)
        {
        }

        public void DiscardQueuedFrames()
        {
        }

        public void SetProperties(IPropertySet configuration)
        {
        }

        CanvasDevice canvasDevice;

        public void SetEncodingProperties(VideoEncodingProperties encodingProperties, IDirect3DDevice device)
        {
            canvasDevice = CanvasDevice.CreateFromDirect3D11Device(device);
        }


        public void ProcessFrame(ProcessVideoFrameContext context)
        {
            var inputSurface = context.InputFrame.Direct3DSurface;
            var outputSurface = context.OutputFrame.Direct3DSurface;

            using (var inputBitmap = CanvasBitmap.CreateFromDirect3D11Surface(canvasDevice, inputSurface))
            using (var renderTarget = CanvasRenderTarget.CreateFromDirect3D11Surface(canvasDevice, outputSurface))
            using (var ds = renderTarget.CreateDrawingSession())
            {   
                ds.DrawImage(inputBitmap);
                
                uint ww = inputBitmap.SizeInPixels.Width;
                uint hh = inputBitmap.SizeInPixels.Height;
                Rect rx = new Rect(0, 0, ww, hh);
                ds.DrawRectangle(rx, Colors.White);

                uint ww2 = ww-2;
                uint hh2 = hh-2;
                Rect rxInner = new Rect(1, 1, ww2, hh2);
                ds.DrawRectangle(rx, Colors.Black);
                
            }
            
        }


    }
}




    

