//version KittyMemoryWebcam v0.80

// This sample demostrates recording audio and webcam in addition to screen recording
// The audio or webcam file is merged with the screen recording to produce the final video
// The VideoBasicEffects runtime component also shows how a programmer can use a videofile as input
// and applies Win2D effects (such as blending, color modifications, applying border etc) to each frame
// to produce a modified video file.

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Globalization;
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
            
            SaveFileTextBox.Text = "Pictures\\Saved Pictures\\VideoScreenStudio\\SavedVideo.mp4";
        }

        SimpleRecorder simpleRecorder = null;
        public int errorCode = 0;
        public string messageStr = "";

        public void msg(string str)
        {
            //Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            //{
                messageStr = str;
                canvas2d.Invalidate();
            
            //});
        }
        
        void onCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
         
            if (simpleRecorder == null)
            {
                return;
            }
            
            if (simpleRecorder.gcitem == null)
            {
                args.DrawingSession.DrawEllipse(150, 85, 80, 30, Colors.Black, 3);
                args.DrawingSession.DrawText("Pick Sceeen", 100, 70, Colors.Black);

                return;
            }
            
            
            if (messageStr != "")
            {
                args.DrawingSession.DrawText(messageStr, 20, 70, Colors.Black);

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


            //ver 8.0 Record Audio
            DecideRecordAudioWebcam();
            simpleRecorder.InitAudioRecording();

            //ver 8.1 Webcam Recording
            await simpleRecorder.InitWebcamRecording();

            //await simpleRecorder.InitializeVideoFile();
            await simpleRecorder.InitializeCam();


            //errorCode = simpleRecorder.StartRecording();
            await simpleRecorder.StartRecording();

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


            StartWritingOutputExtended("", 0);

            simpleRecorder.StopRecording();

            //ver 8.1 Webcam Recording
            simpleRecorder.StopWebcamRecording();

            //ver 8.0 Record Audio
            simpleRecorder.StopAudioRecording();


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

            if (simpleRecorder.gcitem == null)
            {
                StartWritingOutput("Need to record a stream. Click 'Pick Screen' and then the 'Record' button.");
                return;
            }

            //await UnpackToMp4();
            await UnpackWithTranscoder();
        }


        public async Task UnpackWithTranscoder()
        {
            StartWritingOutput("Unpacking..");
            
            StorageFolder pictureFolder = KnownFolders.SavedPictures;
            if (simpleRecorder.savefile == null)
            {
                string mp4filename = "SavedVideo" + ".mp4";
                simpleRecorder.savefile = await pictureFolder.CreateFileAsync(
                    mp4filename,
                    CreationCollisionOption.ReplaceExisting);
            }
            
            simpleRecorder.InitStartTranscoder();

            //Upon completion, transcodeCompleted is called to rename the 
            //transcoded file to simpleRecorder.savefile
        }

        public void transcodeProgress(IAsyncActionWithProgress<double> asyncInfo, double progressInfo)
        {
            
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                string reportstr = "Transcoding Progress: " + progressInfo.ToString() + " %";
                messageStr = reportstr;
                canvas2d.Invalidate();
                
            });
            
            
        }

        public void transcodeCompleted(IAsyncActionWithProgress<double> asyncInfo, AsyncStatus asyncStatus)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ScreenTextBox.Text = "";
                //SaveFileTextBox.Text = "";
                if (simpleRecorder != null)
                {
                    simpleRecorder.gcitem = null;
                    
                    //Copy Temp File to Save File
                    if (simpleRecorder.tempFile != null)
                    {
                        if ((simpleRecorder.bRecordAudio == false) && (simpleRecorder.bRecordWebcam == false))
                        {

                            //Just Copy Temp Video File to SaveFile
                            if (simpleRecorder.savefile != null)
                            {
                                simpleRecorder.tempFile.MoveAndReplaceAsync(simpleRecorder.savefile);
                                simpleRecorder.tempFile = null;
                                TextOutput.Text += System.Environment.NewLine + "Saving file to " + simpleRecorder.savefile.Path;

                                TextOutput.Text += System.Environment.NewLine + "Clearing Memory Buffers.....";
                                simpleRecorder.ClearMemoryBuffers();
                                TextOutput.Text += "  Done.";
                            }
                        }
                        else if ((simpleRecorder.bRecordAudio == true) && (simpleRecorder.bRecordWebcam == false))
                        {
                            //Merge Temp Video File and Audio File
                            if (simpleRecorder.savefile != null)
                            {
                                TextOutput.Text += System.Environment.NewLine + "Clearing Memory Buffers.....";
                                simpleRecorder.ClearMemoryBuffers();

                                simpleRecorder.MergeAudioVideo(simpleRecorder.tempAudioFile, simpleRecorder.tempFile, simpleRecorder.savefile);
                                
                            }

                        }
                        else if ((simpleRecorder.bRecordAudio == false) && (simpleRecorder.bRecordWebcam == true))
                        {
                            //Merge Temp Video File and Audio File
                            if (simpleRecorder.savefile != null)
                            {
                                TextOutput.Text += System.Environment.NewLine + "Clearing Memory Buffers.....";
                                simpleRecorder.ClearMemoryBuffers();

                                simpleRecorder.MergeAudioVideoWebcam(simpleRecorder.tempWebcamFile, simpleRecorder.tempFile, simpleRecorder.savefile);
                                

                            }

                        }
                    }

                    
                }

            });
        }

        //////////////////////
        ///// Audio Recorder
        //////////////////////
        public void mergeProgress(IAsyncOperationWithProgress<TranscodeFailureReason, double> asyncInfo, double progressInfo)
        {

            NumberFormatInfo nfi = new CultureInfo("en-US", false).NumberFormat;
            nfi.NumberDecimalDigits = 0;
            string msgstr = " ..." + progressInfo.ToString("N", nfi) + "%";
            StartWritingOutput(msgstr, 2);

        }

        public void mergeCompleted(IAsyncOperationWithProgress<TranscodeFailureReason, double> asyncInfo, AsyncStatus asyncStatus)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {   
                
                TextOutput.Text += System.Environment.NewLine + "Saving merge file to " + simpleRecorder.savefile.Path;
                TextOutput.Text +=  "   (Done)";
                
                TextOutput.Text += System.Environment.NewLine + "Deleting Temporary Video File";                
                simpleRecorder.tempFile.DeleteAsync();

                TextOutput.Text += System.Environment.NewLine + "Deleting Temporary Audio File";                
                simpleRecorder.tempAudioFile.DeleteAsync();

                simpleRecorder.tempFile = null;
                simpleRecorder.tempAudioFile = null;
            });
            
        }

        //////////////////////
        ///// Audio Recorder
        //////////////////////

        //////////////////////
        ///// Webcam
        //////////////////////
        public void mergeWebcamProgress(IAsyncOperationWithProgress<TranscodeFailureReason, double> asyncInfo, double progressInfo)
        {

            
            NumberFormatInfo nfi = new CultureInfo("en-US", false).NumberFormat;
            nfi.NumberDecimalDigits = 0;
            string msgstr = " ..." + progressInfo.ToString("N", nfi) + "%";
            StartWritingOutput(msgstr, 2);
            
        }

        public void mergeWebcamCompleted(IAsyncOperationWithProgress<TranscodeFailureReason, double> asyncInfo, AsyncStatus asyncStatus)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {

                TextOutput.Text += System.Environment.NewLine + "Saving merge file to " + simpleRecorder.savefile.Path;
                TextOutput.Text += "   (Done)";

                TextOutput.Text += System.Environment.NewLine + "Deleting Temporary ScreenVideo File";
                simpleRecorder.tempFile.DeleteAsync();

                TextOutput.Text += System.Environment.NewLine + "Deleting Temporary Webcam File";
                simpleRecorder.tempWebcamFile.DeleteAsync();

                simpleRecorder.tempFile = null;
                simpleRecorder.tempWebcamFile = null;
            });

        }
        

        //////////////////////
        ///// Webcam
        //////////////////////



        public void StartWritingReport(string msgstr,int add=0)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                if (add==1)
                    TextReport.Text += System.Environment.NewLine +  msgstr;
                else 
                    TextReport.Text = msgstr;
            });
        }

        public void StartWritingOutput(string msgstr,int add=0)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                
                if (add == 1)
                {
                        TextOutput.Text += System.Environment.NewLine + msgstr;

                }
                else if (add == 2)
                {
                        TextOutput.Text +=  msgstr;

                }
                else
                    TextOutput.Text = msgstr;
            });
        }

        public void StartWritingOutputExtended(string msgstr, int add = 0)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                if (add == 1)
                    TextOutputExtended.Text += System.Environment.NewLine + msgstr;
                else
                    TextOutputExtended.Text = msgstr;
            });
        }

        private async void Pick_Click(object sender, RoutedEventArgs e)
        {
            await PickScreen();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            await PickSaveFile();
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

        //This is called only when the record button is clicked
        //other operations do not affect the status of bRecordAudio
        public void DecideRecordAudioWebcam()
        {
            if (simpleRecorder == null)
                return;
            
            //if (OptionsPage.gRecAudio.IsChecked == true)
            if (RecAudio.IsChecked==true)
            {
                simpleRecorder.bRecordAudio = true;
                simpleRecorder.bRecordWebcam = false;
            }
            else
                simpleRecorder.bRecordAudio = false;

            //if (OptionsPage.gRecWebcam.IsChecked == true)
            if (RecWebcam.IsChecked == true)
            {
                simpleRecorder.bRecordWebcam = true;
                simpleRecorder.bRecordAudio = false;
            }
            else
                simpleRecorder.bRecordWebcam = false;
                
        }

        private void RecAudio_Click(object sender, RoutedEventArgs e)
        {
            if (RecAudio.IsChecked == true)
            {
                RecWebcam.IsChecked = false;
            }

        }

        private void RecWebcam_Click(object sender, RoutedEventArgs e)
        {
            if (RecWebcam.IsChecked == true)
            {
                RecAudio.IsChecked = false;
            }


        }
    }
}
