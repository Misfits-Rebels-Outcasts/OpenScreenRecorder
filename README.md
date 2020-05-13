# Open Screen Recorder
A Modern Open Source UWP Screen Recorder

**Open Screen Recorder** (Open Source) is a modern UWP Screen Recorder written in
Windows.Graphics.Capture API. It demonstrates how a programmer can take 
advantage of these new APIs to produce a screen recorder in minutes. 
In addition to the Windows Graphics Capture API, the recorder also utilizes
 Win 2D , and MediaComposition (Windows.Media.Editing). 


 [**Update** KittyMemoryWebcam_v0.80] (#update-v080)
 
 [**Important Update**  KittyMemoryTranscode_v0.75](#update-v075)
  
 

 <kbd><img src="https://github.com/TechnoRiver/OpenScreenRecorder/blob/master/images/OpenScreenRecorder.png"></kbd>

All these APIS are quite recent releases in the .NET Core framework for Windows.
Some of them take advantage of GPU and hardware acceleration to deliver 
high performance results. As the tool is written in .NET Core, 
a programmer may also easily compile the project to ARM and ARM64 using Visual Studio. 
Currently, the project has been tested to run on  
Windows 10 (version 1809). To compile in Windows, 
after downloading the project, you may want to set the Solution Platform to x86 or x64.

<kbd><img src="https://github.com/TechnoRiver/OpenScreenRecorder/blob/master/images/SolutionPlatform.png"></kbd>

The way the code works together represents a very useful skillset for a programmer 
looking into creating media softwares using modern APIs for UWP Windows Apps.
While tiny fragments of how these APIs work individually exists on official samples,
the author understands (at the time of writing) , there is a lack of such examples 
on how these media APIs integrate together into a working software.

While at current state, the recorder is written more like a 
sample rather than a robust software capable of handling all kinds of situations, 
this simple tool has taken the author a painstaking period of over 2 months just to put together
a small task of gathering frames in IDirect3DSurface and saving them into 
a MPEG4 movie.

The UWP screen recorder sample is designed to be resuable and simple. It works in two stages,
the first stage being capturing the frames, compressing them and dumping them into a filestream or memory.
A thread has been create specially to retrieve the individual captured frames (IDirect3DSurface) and compressing
them using Win2D into PNG files. The compressed data is then written to disk with a filestream.
After the user clicked the 'Stop' button to stop the recording, he / she may proceed to the
second stage to encode the saved stream into a MPEG (mp4) file.

By separating the functioning of the recorder into two stages make the tool very modular 
and reusable. For example, a programmer can improve upon the first stage to use other forms of compression, which has the potential 
to speed up its efficiency or speed of recording. As for the second stage, the encoding process consists of just a 
small block of code to unpack each frame from the filestream and add each frame as MediaClips to the
MediaCompositor to produce a MPEG file.

The author has experimented with up to 20 scenarios for configuring the UWP screen recorder.
For example, the separate thread for dumping frames is removed to attempt to further simplify the design.
Also, the two separate stages for recording and recording are combined into one so that all
the recording and compressing work can be placed into a single event. Futhermore, the compressing
and decompressing of frames are removed so that each frame recorded can be directly written to the
MPEG file. However, results show that such configurations are not able to sustain recording 
for more than one minute. The exact cause is still unknown as using all these different new APIs
together constitute a big challenge and finding the bug becomes chiefly a trial and error affair
as documentation proves to be inadequate. Futhermore, once the recording starts,
 the user interactivity becomes quite bad, and the CPU seems to have grabbed all resources
to do the recording and compression, rendering activities for other apps 
(such as typing and menu selection) unworkable.

After much tinkering around, the author decides to release two versions of the recorder 
known as v0.71 (PuppyDisk), and v0.72 (KittyMemory). These two versions managed to avoid many of the problems
mentioned in the above paragraph. The former dumps all frames into a filestream 
while the latter dumps all frames into the memory. Both versions seems able to sustain stable recordings for at least a few minutes.
The former may still give low frame rates when performing full screen recordings, 
while the latter offers better performance but at the price of being a memory hog.
 
For the latter version (v0.72 KittyMemory), while some may argue that dumping all recorded frames 
into memory (insted of the disk) is very wasteful of resources; however, considering most modern PCs 
will be having 8GB of memory or more,  especially in high performance business machines or gaming machines, 
recording the screen for like 10 minutes will probably take less than 1 GB of memory,
but doing will give very stable rates of recording and decent interactivity for the user.


**Usage**

<kbd><img src="https://github.com/TechnoRiver/OpenScreenRecorder/blob/master/images/Usage.png"></kbd>

Note, after (stopping) the recording , the user will still need to manually click the 
'Unpack' button to encode the compressed frames into a MPEG (mp4) file. This step may require 
even more time than the recording duration. For example, if the recording of video takes 
one minute, the time required for the second step may take an additional two minutes.
While this design seems to require an extra step / stage / waiting time, it is through
 such configuration that the user can be assured of the highest frame rates, 
sustainable recording and acceptable interactivity during recording.

**Potential for Direct3D and Win2D Renderings**

This project has the potential to be widely adopted in many uses which involve 
the rendering of Direct3D Surfaces into a movie. Editing a frame in raw RGB / BGRA format, 
such as adding custom annotations, advanced movie effects (such as composition with particles systems)
is a very common scenario encountered by many programmers who wish to produce a movie 
from their existing animation or graphics software. In this project, the editing of a image 
with the popular module Win2D (C#) and saving them into a movie has been demonstrated.

**Limitations**

This project currently utilizes the MediaComposition (namespace Windows.Media.Editing) API
to produce a MPEG movie. While this method is extremely easy with just a few lines of code,
there is a concern that encoding a large video, say, with more than 1000 frames 
can be slow and unstable. In the near future, an improved version using Transcoding API 
will be utilized to make the encoding more robust.


### Update v0.75
**KittyMemoryTranscode_v0.75** (Demonstrating Transcode API with IDirect3DSurface)

By far, this is the most valuable contribution among all the previous releases. 
This update uses the Transcode API to unpack frames from the memory buffers and encode them into
MP4. This method, the author believe, is much more robust compared to previous versions ,
 which instead uses the MediaComposition to produce the video file.

**Importance:**

The value of this update lies in a little piece of code that offers the programmer the 
ability to render in offscreen animated frames of Win2D (CanvasRenderTarget or IDirect3DSurface)
into a MP4 video. The author considers this simple feature itself constitute
 an important capability that could enpower a suite of applications to fully
produce media files.

Examples of applications that immediately come to the author's mind includes video editing applications
that applies Win2D effects to selected regions of a video recorded by cellphone. If you run
the [Win2D Gallery](https://www.microsoft.com/en-us/p/win2d-example-gallery/9nblgggxwt9f?activetab=pivot:overviewtab) samples, you will also notice many cool things that are easily produced with 
Win2D such as Bitmap Effects, WebCam, HLSL and Particles Systems animations can all be 
**composited** with the CanvasRenderTarget and rendered into a movie.

The author understands these types of media production applications may
still be able to make a little revenue in today's collapsed world of software prices
 ever since low cost apps flooded the market and drove out many small independent developers.
The research on this simple capablity has taken the author more than two weeks, but 
the potential of what this code is able do is definitely worth the effort.

**Bugs**

There are still some bugs in this release as the author forgets to free memory buffers
after the transcoding operation. This is quite trivial and will be fixed in the next release.


### Update v0.80

**KittyMemoryWebcam_v0.80**
**VideoBasicEffects_v0.1**

This version demostrates recording audio and webcam in addition to screen recording.
The audio or webcam file is recorded separately (as another file) and merged with the screen recording to produce the final video file.
With the MediaCapture API, the recording of audio from microphone or video from webcam is easily
achieved with just a few lines of code.

**Video Editing Software**

Furthermore, the sample makes use of a separate Windows Runtime Component (.winmd file) to add 
video effect to the webcam recording. Except for the inconvenience of having to create the
video effect in a different project, it shows how easy it is to create software that modify
existing videos with Win2D (such as blending, color modifications, edge detection, border etc)
by making change to each frame. 



**Credits**

At the time of this writing, other samples already exist, and some parts of this project has been referenced from them.

Screen Capture sample:
https://docs.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture

Simple Screen Recorder
https://github.com/robmikh/SimpleRecorder
https://github.com/robmikh/SimpleRecorder
