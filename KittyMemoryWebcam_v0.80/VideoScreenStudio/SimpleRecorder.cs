using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using VideoBasicEffects;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

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

        //Audio Variables
        public bool bRecordAudio = false;

        //Webcam Variables
        public bool bRecordWebcam = false;

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

        public async Task StartRecording()
        {
            if (framePool == null)
            {
                if (parent != null)
                    parent.StartWritingOutput("FramePool is null", 1);

                return;
            }

            if (gcitem == null)
            {
                if (parent != null)
                    parent.StartWritingOutput("Graphics Capture Item is null", 1);

                return;
            }

            counter = 0;
            threadcounter = 0;
            totalMemoryUsed = 0;

            framePool.FrameArrived += OnFrameArrived;
            isRecording = true;

            if (parent != null)
                parent.StartWritingOutput("Recording Starts...", 2);


            //if (bRecordWebcam)
            //{                
            //    if (tempWebcamFile != null)
            //    {
            //        MediaEncodingProfile webcamProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Qvga);
            //        await mediaCapture.StartRecordToStorageFileAsync(webcamProfile, tempWebcamFile);
            //    }                
            //}
            //else if (bRecordAudio)
            //{                
            //    if (memoryAudioStream != null)
            //    {
            //        await mediaCapture.StartRecordToStreamAsync(
            //                        MediaEncodingProfile.CreateMp3(AudioEncodingQuality.Auto), memoryAudioStream);
            //    }                
            //}


            session = framePool.CreateCaptureSession(gcitem);
            session.StartCapture();

            return;

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
            Thread.Sleep(50);

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
                parent.StartWritingOutputExtended("Temporary Video File : " + tempFile.Path, 1);

            IRandomAccessStream destStream = await tempFile.OpenAsync(FileAccessMode.ReadWrite);

            int width = 320;
            int height = 200;

            if (gcitem != null)
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
                    parent.StartWritingOutput("Invalid Frame", 1);

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
                        parent.StartWritingOutput("CBM Error : " + e.Message, 1);
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

                    //Clearing memory buffer for this frame //Crash
                    //unpackList[frameCounter].compressedBuffer = null;
                    //unpackItem.compressedBuffer = null;

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


        public void ClearMemoryBuffers()
        {
            if (unpackList != null)
            {
                int len = unpackList.Count;
                for (int i = 0; i < len; i++)
                {
                    unpackList[i].compressedBuffer = null;
                }
            }

        }



        private async Task<StorageFile> GetTempOutputFile(int isWebcam = 0)
        {
            //Need ApplicationData.Current.TemporaryFolder for transcoder to work ?
            StorageFolder tempfolder = ApplicationData.Current.TemporaryFolder;

            //this folder not working, transcoder stuck ?
            //StorageFolder tempfolder = KnownFolders.PicturesLibrary; 

            Random random = new Random();
            int num = random.Next(80000) + 10000;

            string filename = "tempFile" + num.ToString() + ".mp4";
            if (isWebcam == 1)
                filename = "webcamFile" + num.ToString() + ".mp4";

            StorageFile temporaryFile = await tempfolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);

            return temporaryFile;
        }

        ///////////////////
        /// Transcoder Code
        ///////////////////

        //////////////////////
        ///// Audio Recorder
        //////////////////////

        public MediaCapture mediaCapture = null;
        InMemoryRandomAccessStream memoryAudioStream = null;
        public StorageFile tempAudioFile = null;

        public async void InitAudioRecording()
        {
            if (bRecordAudio == false)
            {
                return;
            }

            if (parent != null)
                parent.StartWritingOutput("Initializing Audio Recording...  ", 1);

            try
            {
                MediaCaptureInitializationSettings settings =
                new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Audio
                };

                mediaCapture = new MediaCapture();
                await mediaCapture.InitializeAsync(settings);

                memoryAudioStream = new InMemoryRandomAccessStream();

                await mediaCapture.StartRecordToStreamAsync(
                MediaEncodingProfile.CreateMp3(AudioEncodingQuality.Auto), memoryAudioStream);

                tempAudioFile = null;

            }
            catch (Exception e)
            {
                if (parent != null)
                    parent.StartWritingOutput("Record Audio Error : " + e.Message, 1);

            }
        }

        public async void StopAudioRecording()
        {
            if (bRecordAudio == false)
            {
                return;
            }

            //crash on this line //due to invaid random access stream

            if (mediaCapture == null)
            {
                if (parent != null)
                    parent.StartWritingOutput("Error Writing Audio", 1);

                return;

            }

            await mediaCapture.StopRecordAsync();

            await SaveAudioToFile();

            mediaCapture.Dispose();
            mediaCapture = null;
        }

        public async Task SaveAudioToFile()
        {

            if (parent != null)
                parent.StartWritingOutputExtended("Writing Audio... please wait...", 0);

            Random random = new Random();
            int num = random.Next(80000) + 10000;
            string audiofilename = "audioFile" + num.ToString() + ".mp3";

            IRandomAccessStream audioStream = memoryAudioStream.CloneStream();
            StorageFolder tempfolder = ApplicationData.Current.TemporaryFolder;
            StorageFile storageFile = await tempfolder.CreateFileAsync(
            audiofilename, CreationCollisionOption.GenerateUniqueName);



            using (IRandomAccessStream fileStream =
            await storageFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                await RandomAccessStream.CopyAndCloseAsync(
                audioStream.GetInputStreamAt(0), fileStream.GetOutputStreamAt(0));
                await audioStream.FlushAsync();
                audioStream.Dispose();
            }


            tempAudioFile = storageFile;

            if (parent != null)
                parent.StartWritingOutputExtended("Audio File : " + storageFile.Path, 1);

        }

        public async Task MergeAudioVideo(StorageFile audioFile, StorageFile videoFile, StorageFile outputFile)
        {
            if (audioFile == null)
            {
                if (parent != null)
                    parent.StartWritingOutput("Audio File is null", 1);
                return;
            }

            if (videoFile == null)
            {
                if (parent != null)
                    parent.StartWritingOutput("Video File is null", 1);
                return;
            }

            if (outputFile == null)
            {
                if (parent != null)
                    parent.StartWritingOutput("Output File is null (not specified)", 1);
                return;
            }


            if (!audioFile.IsAvailable)
            {
                if (parent != null)
                    parent.StartWritingOutput("Audio File is not available", 1);
                return;
            }

            if (!videoFile.IsAvailable)
            {
                if (parent != null)
                    parent.StartWritingOutput("Video File is not available", 1);
                return;
            }

            if (parent != null)
                parent.StartWritingOutput("Merging temporary video and audio files..", 1);

            MediaClip mediaClip = await MediaClip.CreateFromFileAsync(videoFile);
            BackgroundAudioTrack backgroundAudioTrack = await BackgroundAudioTrack.CreateFromFileAsync(audioFile);

            MediaComposition mediaComposition = new MediaComposition();
            mediaComposition.Clips.Add(mediaClip);
            mediaComposition.BackgroundAudioTracks.Add(backgroundAudioTrack);

            if (outputFile != null)
            {
                var mergeOperation = mediaComposition.RenderToFileAsync(outputFile, MediaTrimmingPreference.Precise);
                mergeOperation.Progress += mergeProgress;
                mergeOperation.Completed += mergeCompleted;
            }

        }

        private void mergeCompleted(IAsyncOperationWithProgress<TranscodeFailureReason, double> asyncInfo, AsyncStatus asyncStatus)
        {
            if (parent != null)
            {
                parent.mergeCompleted(asyncInfo, asyncStatus);
            }
        }

        private void mergeProgress(IAsyncOperationWithProgress<TranscodeFailureReason, double> asyncInfo, double progressInfo)
        {
            if (parent != null)
            {
                parent.mergeProgress(asyncInfo, progressInfo);
            }
        }

        ////////////////////
        /// Webcam
        //////////////////////

        public StorageFile tempWebcamFile = null;
        private static async Task<DeviceInformation> FindCamera(Panel panel)
        {

            DeviceInformationCollection videoCaptureDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            foreach (var device in videoCaptureDevices)
            {
                if (device.EnclosureLocation.Panel == panel)
                    return device;
            }


            if (videoCaptureDevices.Count > 0)
                return videoCaptureDevices[0];
            else
                return null;
        }


        public async void StopWebcamRecording()
        {
            if (bRecordWebcam == false)
            {
                return;
            }

            
            if (mediaCapture == null)
            {
                if (parent != null)
                    parent.StartWritingOutput("Error in Webcam MediaCapture", 1);

                return;

            }

            if (tempWebcamFile != null)
            {
                if (parent != null)
                    parent.StartWritingOutputExtended("Webcam File : " + tempWebcamFile.Path, 1);

            }


            await mediaCapture.StopRecordAsync();

            mediaCapture.Dispose();
            mediaCapture = null;



        }


        public async Task InitWebcamRecording()
        {
            if (bRecordWebcam == false)
            {
                return;
            }

            tempWebcamFile = null;

            if (parent != null)
                parent.StartWritingOutput("Initializing Webcam... Please get ready...  ", 1);


            DeviceInformation webcamDevice = await FindCamera(Windows.Devices.Enumeration.Panel.Back);

            if (webcamDevice == null)
            {
                if (parent != null)
                    parent.StartWritingOutput("Webcam Error : No Device Found", 1);

                return;
            }

            if (mediaCapture == null)
            {
                mediaCapture = new MediaCapture();
                mediaCapture.RecordLimitationExceeded += mediaCaptureRecordLimitationExceeded;
                mediaCapture.Failed += mediaCaptureFailed;
            }

            try
            {
                MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings { VideoDeviceId = webcamDevice.Id };
                await mediaCapture.InitializeAsync(settings);

            }
            catch (Exception e)
            {
                if (parent != null)
                    parent.StartWritingOutput("Init Webcam Error : " + e.Message, 1);

            }


            StorageFile webcamFile = await GetTempOutputFile(1);


            if (parent != null)
                parent.StartWritingOutputExtended("Temporary Webcam File : " + webcamFile.Path, 1);

            //MediaEncodingProfile webcamProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
            MediaEncodingProfile webcamProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Qvga);
            tempWebcamFile = webcamFile;

            await mediaCapture.StartRecordToStorageFileAsync(webcamProfile, webcamFile);

        }

        private void mediaCaptureFailed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            if (parent != null)
                parent.StartWritingOutputExtended("Record Webcam Failed : " + errorEventArgs.Message, 1);

            mediaCapture.Dispose();
            mediaCapture = null;
        }

        private async void mediaCaptureRecordLimitationExceeded(MediaCapture sender)
        {
            if (parent != null)
                parent.StartWritingOutputExtended("Webcam Record Limit Exceeded : ", 1);

            await mediaCapture.StopRecordAsync();


        }

        public async Task MergeAudioVideoWebcam(StorageFile webcamFile, StorageFile videoFile, StorageFile outputFile)
        {

            if (webcamFile == null)
            {
                if (parent != null)
                    parent.StartWritingOutput("Webcam File is null", 1);
                return;
            }

            if (videoFile == null)
            {
                if (parent != null)
                    parent.StartWritingOutput("Video File is null", 1);
                return;
            }

            if (outputFile == null)
            {
                if (parent != null)
                    parent.StartWritingOutput("Output File is null (not specified)", 1);
                return;
            }


            if (!webcamFile.IsAvailable)
            {
                if (parent != null)
                    parent.StartWritingOutput("Webcam File is not available", 1);
                return;
            }

            if (!videoFile.IsAvailable)
            {
                if (parent != null)
                    parent.StartWritingOutput("Video File is not available", 1);
                return;
            }

            if (parent != null)
                parent.StartWritingOutput("Merging temporary video and webcam files..", 1);

            try
            {


                MediaClip mediaClip = await MediaClip.CreateFromFileAsync(videoFile);
                MediaClip webcamClip = await MediaClip.CreateFromFileAsync(webcamFile);
    

                //the BorderVideoEffects need to be in a separate Windows Component Runtime
                webcamClip.VideoEffectDefinitions.Add(new VideoEffectDefinition(typeof(BorderVideoEffect).FullName));

                int xoffset = 20;
                int yoffset = 30;
                double overlayOpacity = 1.0;
                double overlayHeight = 150;
                Rect overlayRect;
                VideoEncodingProperties webcamEncodingProperties = webcamClip.GetVideoEncodingProperties();
                overlayRect.Height = overlayHeight;
                double widthToHeightAspectRatio = ((double)webcamEncodingProperties.Width / (double)webcamEncodingProperties.Height);
                overlayRect.Width = widthToHeightAspectRatio * overlayHeight;
                overlayRect.X = xoffset;
                overlayRect.Y = yoffset;

                MediaOverlay mediaOverlay = new MediaOverlay(webcamClip);
                mediaOverlay.Position = overlayRect;
                mediaOverlay.Opacity = overlayOpacity;
                mediaOverlay.AudioEnabled = true;

                MediaOverlayLayer mediaOverlayLayer = new MediaOverlayLayer();
                mediaOverlayLayer.Overlays.Add(mediaOverlay);

                MediaComposition mediaComposition = new MediaComposition();
                mediaComposition.Clips.Add(mediaClip);
                mediaComposition.OverlayLayers.Add(mediaOverlayLayer);


                if (outputFile != null)
                {

                    var mergeOperation = mediaComposition.RenderToFileAsync(outputFile, MediaTrimmingPreference.Precise);
                    mergeOperation.Progress += mergeWebcamProgress;
                    mergeOperation.Completed += mergeWebcamCompleted;
                }


            }
            catch (Exception e)
            {
                if (parent != null)
                {
                    parent.StartWritingOutput("Merge Error :" + e.Message, 1);
                }
            }


        }

        private void mergeWebcamCompleted(IAsyncOperationWithProgress<TranscodeFailureReason, double> asyncInfo, AsyncStatus asyncStatus)
        {
            if (parent != null)
            {
                parent.mergeWebcamCompleted(asyncInfo, asyncStatus);
            }
        }

        private void mergeWebcamProgress(IAsyncOperationWithProgress<TranscodeFailureReason, double> asyncInfo, double progressInfo)
        {
            if (parent != null)
            {
                parent.mergeWebcamProgress(asyncInfo, progressInfo);
            }
        }


    }

}
