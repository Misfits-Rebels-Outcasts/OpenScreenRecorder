using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace VideoScreenStudio
{
    class SimpleRecorder
    {
        public bool isRecording = false;
        public GraphicsCaptureItem gcitem = null;
        public StorageFile savefile = null;
        public StorageFile streamFile = null;

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

        MainPage parent = null;

        //Encoder variables
        public IRandomAccessStream videostream = null;
        public InMemoryRandomAccessStream memorystream = null;

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

            tempFile = null;

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
            //gcitem = null; //cannot set to null until app has finished encoding

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
            //strange..need  this to cause more frames to arrive?
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

            ///need to handle device lost

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


                        unpackItem.compressedBuffer = new Windows.Storage.Streams.Buffer((uint)inputstream.Size);
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

            if (elpasedTime.TotalSeconds > 0)
                debugstrx += "  Frame Rate (fps) : " + (len / (double)elpasedTime.TotalSeconds).ToString();

            if (parent != null)
                parent.StartWritingReport(debugstrx);

            //await UnpackVideoStream();
        }

        
        
        ////////////////////
        /// Transcoder Code
        ////////////////////

        public int frameCounter = 0;
        public TimeSpan Timestamp = TimeSpan.Zero;
        public StorageFile tempFile = null;

        public async Task InitStartTranscoder()
        {
            if (parent != null)
                parent.StartWritingOutput("Initialize Transcoder", 1);


            tempFile = await GetTempOutputFile();            
            if (parent != null)
                parent.StartWritingOutputExtended("Temporary Output : " + tempFile.Path, 0);

            IRandomAccessStream destStream = await tempFile.OpenAsync(FileAccessMode.ReadWrite);

            int width = 320;
            int height = 200;

            if (gcitem!=null)
            {
                width = gcitem.Size.Width;
                height = gcitem.Size.Height;
            }

            frameCounter = 0;
            Timestamp = TimeSpan.Zero;

            VideoEncodingProperties videoSourceProperties = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Rgb32, (uint)width, (uint)height);
            VideoStreamDescriptor videoSourceDescriptor = new VideoStreamDescriptor(videoSourceProperties);
            
            MediaStreamSource mediaStreamSource = new MediaStreamSource(videoSourceDescriptor);
            mediaStreamSource.BufferTime = TimeSpan.FromSeconds(0);
            mediaStreamSource.Starting += OnMSSStarting;
            mediaStreamSource.SampleRequested += OnMSSSampleRequested;
            mediaStreamSource.SampleRendered += OnMSSSampleRendered;
            //mediaStreamSource.CanSeek = false;

            MediaTranscoder mediaTranscoder = new MediaTranscoder();
            mediaTranscoder.HardwareAccelerationEnabled = true;
            
            ////////////////////
            //Start Transcoding
            MediaEncodingProfile destProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
            PrepareTranscodeResult transcodeOperation = await mediaTranscoder.PrepareMediaStreamSourceTranscodeAsync(mediaStreamSource, destStream, destProfile);

            //await transcode.TranscodeAsync();
            var rendering = transcodeOperation.TranscodeAsync();
            rendering.Progress += progressHandler;
            rendering.Completed += completedHandler;
            
        }
        
        
        private void completedHandler(IAsyncActionWithProgress<double> asyncInfo, AsyncStatus asyncStatus)
        {
            if (parent != null)
            {
                parent.transcodeCompleted(asyncInfo, asyncStatus);
            }
        }
        

        private void progressHandler(IAsyncActionWithProgress<double> asyncInfo, double progressInfo)
        {
            if (parent != null)
            {
                parent.transcodeProgress(asyncInfo, progressInfo);
            }
        }
        

        private void OnMSSStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            if (parent != null)
                parent.StartWritingOutput("OnStarting", 1);
            args.Request.SetActualStartPosition(Timestamp);

        }

        
        private void OnMSSSampleRendered(MediaStreamSource sender, MediaStreamSourceSampleRenderedEventArgs args)
        {
            if (parent != null)
                parent.StartWritingOutput("Written Frame : " + (frameCounter).ToString(), 1);
                
        }
        


        /// <summary>
        /// //Encoding a Win2D surface (CanvasRenderTarget) as a video frame
        /// </summary>
        private async void OnMSSSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {

            if (parent != null)
                parent.StartWritingOutput("OnSampleRequested " + frameCounter.ToString(), 0);


            if (unpackList == null)
            {
                if (parent != null)
                    parent.StartWritingOutput("Unpack List Null Error!", 1);

                //this will stop the encoding
                args.Request.Sample = null;
                return;
            }


            int len = unpackList.Count;
            if (frameCounter >= len)
            {
                if (parent != null)
                    parent.StartWritingOutput("Encoding Completed.", 1);

                //this will stop the encoding
                args.Request.Sample = null;
                return;
            }

            if ((frameCounter < 0) || (0 == len))
            {
                if (parent != null)
                    parent.StartWritingOutput("Invalid Frame",1);

                //this will stop the encoding
                args.Request.Sample = null;
                return;
            }


            //need deferral because CanvasBitmap.LoadAsync takes some time to complete ?
            var deferral = args.Request.GetDeferral();

            ///
            UnpackItem unpackItem = unpackList[frameCounter];
            Windows.Storage.Streams.Buffer buffer = unpackItem.compressedBuffer;

            InMemoryRandomAccessStream inMemoryRandomAccessStream = null;
            using (inMemoryRandomAccessStream = new InMemoryRandomAccessStream())
            {
                await inMemoryRandomAccessStream.WriteAsync(buffer);
                await inMemoryRandomAccessStream.FlushAsync();
                inMemoryRandomAccessStream.Seek(0);


                CanvasBitmap tempBitmap = null;
                try
                {
                    tempBitmap = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), inMemoryRandomAccessStream);
                }
                catch (Exception e)
                {
                    if (parent != null)
                        parent.StartWritingOutput("CBM Error : " + e.Message,1);
                }
                

                if (tempBitmap != null)
                {

                    CanvasRenderTarget canvasRenderTarget = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), tempBitmap.SizeInPixels.Width, tempBitmap.SizeInPixels.Height, tempBitmap.Dpi);
                    using (CanvasDrawingSession session = canvasRenderTarget.CreateDrawingSession())
                    {
                        session.Clear(Colors.Black);
                        //session.DrawEllipse(new System.Numerics.Vector2(120 + frameCounter * 2, 100), 30, 20, Colors.White);
                        session.DrawImage(tempBitmap);
                    }

                    TimeSpan timeLapsed = unpackItem.frameTime;
                    Timestamp += timeLapsed;

                    //set sample after defferal ? nope ....stop at 1st frame...
                    MediaStreamSample sample = MediaStreamSample.CreateFromDirect3D11Surface(canvasRenderTarget, Timestamp);
                    args.Request.Sample = sample;

                    deferral.Complete();
                }
                else
                {
                    args.Request.Sample = null;
                    deferral.Complete();

                }
                    
                frameCounter++;

            }

        }

        private async Task<StorageFile> GetTempOutputFile()
        {
            //Need ApplicationData.Current.TemporaryFolder for transcoder to work ?
            StorageFolder tempfolder = ApplicationData.Current.TemporaryFolder;

            //this folder not working, transcoder stuck ?
            //StorageFolder tempfolder = KnownFolders.PicturesLibrary; 

            Random random = new Random();
            int num = random.Next(80000) + 10000;            
            string filename = "tempFile" + num.ToString() + ".mp4";
            StorageFile temporaryFile = await tempfolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
            
            return temporaryFile;
        }
        
        ///////////////////
        /// Transcoder Code
        ///////////////////

    }


}
