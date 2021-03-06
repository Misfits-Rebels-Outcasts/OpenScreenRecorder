﻿using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Storage;
using Windows.Storage.Streams;

namespace VideoScreenStudio
{
    class SimpleRecorder
    {
        public bool isRecording = false;
        public GraphicsCaptureItem gcitem = null;
        public StorageFile savefile = null;        
        MainPage parent = null;

        //Graphics Capture variables
        private Direct3D11CaptureFramePool framePool = null;
        private GraphicsCaptureSession session = null;
        public Direct3D11CaptureFrame currentFrame = null;
        public TimeSpan currentFrameTime = TimeSpan.Zero;
        public DateTime initialRecordTime = DateTime.Now;
        public DateTime previousRecordTime = DateTime.Now;

        //Counters
        public int counter = 0;        
        public int threadcounter = 0;
        public ulong totalMemoryUsed = 0;

        //Win 2d variables
        public CanvasDevice canvasDevice = null;
        
        //Unpacking variables
        public List<UnpackItem> unpackList = null;
        public ulong _currentVideoStreamPos = 0;

        public SimpleRecorder(MainPage mainPage)
        {
            parent = mainPage;

        }

        public async Task<int> InitializeCam()
        {
            if (gcitem == null)
                return 1;

            if (canvasDevice == null)
                canvasDevice = new CanvasDevice();
            
            if (framePool == null)
            {
                /*
                //only 2 frames ... depending on the number of buffers
                framePool = Direct3D11CaptureFramePool.Create(
                 canvasDevice, // D3D device
                 DirectXPixelFormat.B8G8R8A8UIntNormalized, // Pixel format
                 2, // Number of frames
                 gcitem.Size); // Size of the buffers
                 */

                framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    canvasDevice,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    1,
                    gcitem.Size);

            }

            initialRecordTime = DateTime.Now;
            previousRecordTime = DateTime.Now;

            currentFrame = null;

            unpackList = new List<UnpackItem>();
            _currentVideoStreamPos = 0;

            return 0;
        }
        
        public void cleanUpCam()
        {
            session?.Dispose();
            framePool?.Dispose();
            currentFrame?.Dispose();
            canvasDevice?.Dispose();

            session = null;
            framePool = null;
            currentFrame = null;

            canvasDevice = null;
            gcitem = null;

        }

        public int StartRecording()
        {
            if (framePool == null)
                return 1;

            if (gcitem == null)
                return 2;

            counter = 0;
            threadcounter = 0;
            totalMemoryUsed = 0;

            framePool.FrameArrived += OnFrameArrived;
            isRecording = true;

            //parent.msg("Started");

            session = framePool.CreateCaptureSession(gcitem);
            session.StartCapture();

            return 0;

        }

        public int StopRecording()
        {
            if (isRecording == false)
                return 0;

            cleanUpCam();
            isRecording = false;
            
            return 0;
        }
        
        private async void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            //need  this to cause more frames to arrive?
            if (parent != null)
            {
                counter++;
                if (counter > 1000000)
                    counter = 0;
                parent.msg("Arrived : " + counter.ToString());                
            }

            if (isRecording == false)
                return;

            currentFrame = sender.TryGetNextFrame();
            
        }
        
        private void ProceedToRecordNextFrame()
        {
            currentFrame?.Dispose();
        }
        
        public async void SeparateThreadToSaveVideoStream()        
        {
            while (isRecording == true)
            {
                //thread is counting
                //sometimes stuck at thread at frame : 0, depending on the window chosen to record
                //meaning OnFrameArrived is not called
                if (parent != null)
                {
                    DateTime currentTimeLocal = DateTime.Now;
                    TimeSpan elpasedTimeLocal = currentTimeLocal - initialRecordTime;
                    string debugstr = "At frame: " + counter.ToString() + "  Threadcounter: " + threadcounter.ToString();
                    //debugstr += "  StreamSize: " + ((int)(videostream.Size / 1024.0)).ToString() + "KB  TimeElapsed: " + ((int)elpasedTimeLocal.TotalSeconds).ToString();
                    debugstr += "  StreamSize: " + ((int)(totalMemoryUsed / 1024.0)).ToString() + " KB";
                    debugstr += "  TimeElapsed: " + ((int)elpasedTimeLocal.TotalSeconds).ToString();
                    parent.msg(debugstr);
                }

                threadcounter++;
                if (threadcounter > 200000)
                    threadcounter = 0;


                if (currentFrame != null)
                {
                    ///need to handle device lost
                    CanvasBitmap canvasBitmap = CanvasBitmap.CreateFromDirect3D11Surface(
                    canvasDevice,
                    currentFrame.Surface);
                    
                    using (var inputstream = new InMemoryRandomAccessStream())
                    {
                        CancellationToken ct = new CancellationToken();
                        await canvasBitmap.SaveAsync(inputstream, CanvasBitmapFileFormat.Png, 1f).AsTask(ct);
                        ulong currentFrameLength = inputstream.Size;

                        _currentVideoStreamPos = 0;
                        totalMemoryUsed += currentFrameLength;

                        DateTime currentTimeLocal = DateTime.Now;
                        TimeSpan diff = currentTimeLocal - previousRecordTime;
                        previousRecordTime = currentTimeLocal;                        

                        UnpackItem unpackItem = new UnpackItem();
                        unpackItem.pos = _currentVideoStreamPos;
                        unpackItem.length = currentFrameLength;
                        unpackItem.frameTime = diff;

                        
                        unpackItem.compressedBuffer = new Windows.Storage.Streams.Buffer((uint) inputstream.Size);
                        inputstream.Seek(0);
                        await inputstream.ReadAsync(unpackItem.compressedBuffer, (uint)inputstream.Size, InputStreamOptions.None); //read from stream to buffer
                        await inputstream.FlushAsync();
                        
                        unpackList.Add(unpackItem);

                    }

                    currentFrame?.Dispose();
                    currentFrame = null; //need this line so this thread will continue loop when new frame is not yet ready

                }
                else
                    Thread.Sleep(10);

            }
            
            //await CloseVideoStream();

            int len = unpackList.Count;
            DateTime currentTime = DateTime.Now;
            TimeSpan elpasedTime = currentTime - initialRecordTime;
            string debugstrx = "Num frame: " + len.ToString() + "  Threadcounter: " + threadcounter.ToString();
            debugstrx += "  TimeElapsed: " + ((int)elpasedTime.TotalSeconds).ToString();

            if (elpasedTime.TotalSeconds>0)
                debugstrx += "  Frame Rate (fps) : " + (len / (double)elpasedTime.TotalSeconds).ToString();

            if (parent != null)
                parent.StartWritingReport(debugstrx);

            //await  parent.UnpackToMp4();
        }

    }
}
