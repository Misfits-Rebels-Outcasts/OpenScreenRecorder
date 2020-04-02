using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace VideoScreenStudio
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            InitRecorder();

            SaveFileTextBox.Text = "Pictures\\Saved Pictures\\VideoScreenStudio\\Video.mp4";
        }

        SimpleRecorder simpleRecorder = null;
        public int errorCode = 0;
        public string messageStr = "";

        public void msg(string str)
        {
            messageStr = str;
            canvas2d.Invalidate();

        }
        
        void onCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
         
            if (simpleRecorder == null)
            {
                return;
            }
            
            if (simpleRecorder.gcitem == null)
            {
                args.DrawingSession.DrawEllipse(150, 115, 80, 30, Colors.Black, 3);
                args.DrawingSession.DrawText("Pick Sceeen", 100, 100, Colors.Black);

                return;
            }
            
            
            if (messageStr != "")
            {
                args.DrawingSession.DrawText(messageStr, 10, 100, Colors.Black);

            }
                    
        }

        private async void Record_Click(object sender, RoutedEventArgs e)
        {
            if (simpleRecorder == null)
            {
                StartWritingOutput("Null recorder error!");
                return;
            }

            if (simpleRecorder.gcitem == null)
            {
                StartWritingOutput("Pick a screen to record");
                return;
            }

            //await simpleRecorder.InitializeVideoFile();
            await simpleRecorder.InitializeCam();
           
            errorCode = simpleRecorder.StartRecording();

            Thread thread = new Thread(new ThreadStart(simpleRecorder.SeparateThreadToSaveVideoStream));
            thread.Start();

        }

        private async void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (simpleRecorder == null)
            {
                StartWritingOutput("Null recorder error!");
                return;
            }

            if (simpleRecorder.isRecording == false)
                return;

            simpleRecorder.StopRecording();

            StartWritingOutput("Click the 'Unpack Files' button to create MP4 video file");

        }

        private async void UnpackFiles_Click(object sender, RoutedEventArgs e)
        {
            if (simpleRecorder == null)
            {
                StartWritingOutput("Null recorder error!");
                return;
            }

            if (simpleRecorder.isRecording == true)
            {
                StartWritingOutput("Recording stream... click the 'Stop' button first");
                return;
            }
            
            await UnpackToMp4();
            
        }

        private async void Pick_Click(object sender, RoutedEventArgs e)
        {
            await PickScreen();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            await PickSaveFile();
        }


        public async Task UnpackToMp4()
        {
            MediaComposition mediacomposition = new MediaComposition();
            
            StorageFolder pictureFolder = KnownFolders.SavedPictures;
            
            {
                int len = simpleRecorder.unpackList.Count;
                for (int i = 0; i < len; i++)
                {
                    
                    UnpackItem unpackItem = simpleRecorder.unpackList[i];
                    Windows.Storage.Streams.Buffer buffer = unpackItem.compressedBuffer;

                    InMemoryRandomAccessStream inMemoryRandomAccessStream = null;
                    using (inMemoryRandomAccessStream = new InMemoryRandomAccessStream())
                    {
                        await inMemoryRandomAccessStream.WriteAsync(buffer);
                        await inMemoryRandomAccessStream.FlushAsync();

                        inMemoryRandomAccessStream.Seek(0);
                        CanvasBitmap tempBitmap = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), inMemoryRandomAccessStream);
                        if (tempBitmap != null)
                        {
                            CanvasRenderTarget canvasRenderTarget = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), tempBitmap.SizeInPixels.Width, tempBitmap.SizeInPixels.Height, 96);
                            using (CanvasDrawingSession session = canvasRenderTarget.CreateDrawingSession())
                            {
                                session.Clear(Colors.Black);
                                session.DrawImage(tempBitmap);

                                TimeSpan frameTime30Mil = TimeSpan.FromMilliseconds(30f);
                                TimeSpan frameTime = unpackItem.frameTime;
                                //if (frameTime < frameTime30Mil)
                                //    frameTime = frameTime30Mil;

                                MediaClip mediaclip = MediaClip.CreateFromSurface(canvasRenderTarget, frameTime);
                                mediacomposition.Clips.Add(mediaclip);

                            }

                            string str = "Adding Clips " + (i + 1).ToString() + " / " + len.ToString();
                            if (i == len - 1)
                                str += "  ...  Please wait for file rendering  ...";
                            TextOutput.Text = str;

                        }

                    }

                    //free up memory recources as each frame is unpacked
                    if (unpackItem.compressedBuffer != null)
                        unpackItem.compressedBuffer = null;

                } //for 
            }

            StorageFile mp4file = null;
            if (simpleRecorder.savefile != null)
            {
                mp4file = simpleRecorder.savefile;
            }
            else
            {
                string mp4filename = "SavedVideo" + ".mp4";
                mp4file = await pictureFolder.CreateFileAsync(
                    mp4filename,
                    CreationCollisionOption.ReplaceExisting);
            }

            //await mediacomposition.RenderToFileAsync(mp4file, MediaTrimmingPreference.Precise, MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p));
            var rendering = mediacomposition.RenderToFileAsync(mp4file, MediaTrimmingPreference.Precise, MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p));
            rendering.Progress += ProgressReport;
            rendering.Completed += CompletedReport;

        }


        private void ProgressReport(IAsyncOperationWithProgress<TranscodeFailureReason, double> asyncOp, double progress)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                TextOutput.Text = "Rendering To File : " + progress.ToString() + " %";
            });
        }

        
        private void CompletedReport(IAsyncOperationWithProgress<TranscodeFailureReason, double> asyncOp, AsyncStatus status)
        {

            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ScreenTextBox.Text = "";
                //SaveFileTextBox.Text = "";
                if (simpleRecorder!=null)
                {
                    simpleRecorder.gcitem = null;               
                    TextOutput.Text = "Done";
                }

            });
        }
        

        public void StartWritingReport(string msgstr)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                TextReport.Text = msgstr;
            });
        }

        public void StartWritingOutput(string msgstr)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                TextOutput.Text = msgstr;
            });
        }
        

        public async Task PickScreen()
        {
            GraphicsCapturePicker picker = new GraphicsCapturePicker();
            GraphicsCaptureItem item = await picker.PickSingleItemAsync();

            if (item != null)
            {
                if (simpleRecorder!=null)
                {
                    simpleRecorder.gcitem = item;
                    ScreenTextBox.Text = item.DisplayName;
                    
                }
            }
        }

        public async Task PickSaveFile()
        {
            FileSavePicker picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            picker.SuggestedFileName = "video";
            picker.DefaultFileExtension = ".mp4";
            picker.FileTypeChoices.Add("MP4 Video", new List<string> { ".mp4" });

            StorageFile savefile = await picker.PickSaveFileAsync();
            if (savefile != null)
            {
                simpleRecorder.savefile = savefile;
                SaveFileTextBox.Text = savefile.Path;
                
            }
        }
        
        public void InitRecorder()
        {
            simpleRecorder = new SimpleRecorder(this);

        }

        public void CloseRecorder()
        {
            simpleRecorder= null;
        }

        
}
}
