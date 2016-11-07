namespace KinectTestApp
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Numerics;
  using System.Threading.Tasks;
  using Windows.Media.Capture;
  using Windows.Media.Capture.Frames;
  using Windows.Media.Devices.Core;
  using Windows.Perception.Spatial;
  using WindowsPreview.Media.Capture.Frames;

  class mtKinectColorPoseFrameHelper
  {
    public event EventHandler<mtSoftwareBitmapEventArgs> ColorFrameArrived;
    public event EventHandler<mtPoseTrackingFrameEventArgs> PoseFrameArrived;

    public mtKinectColorPoseFrameHelper()
    {
      this.softwareBitmapEventArgs = new mtSoftwareBitmapEventArgs();
    }
    internal async Task<bool> InitialiseAsync()
    {
      bool necessarySourcesAvailable = false;

      // Find all possible source groups.
      var sourceGroups = await MediaFrameSourceGroup.FindAllAsync();

      // We try to find the Kinect by asking for a group that can deliver
      // color, depth, custom and infrared. 
      var allGroups = await GetGroupsSupportingSourceKindsAsync(
        MediaFrameSourceKind.Color,
        MediaFrameSourceKind.Depth,
        MediaFrameSourceKind.Custom,
        MediaFrameSourceKind.Infrared);

      // We assume the first group here is what we want which is not
      // necessarily going to be right on all systems so would need
      // more care.
      var firstSourceGroup = allGroups.FirstOrDefault();

      // Got one that supports all those types?
      if (firstSourceGroup != null)
      {
        this.mediaCapture = new MediaCapture();

        var captureSettings = new MediaCaptureInitializationSettings()
        {
          SourceGroup = firstSourceGroup,
          SharingMode = MediaCaptureSharingMode.SharedReadOnly,
          StreamingCaptureMode = StreamingCaptureMode.Video,
          MemoryPreference = MediaCaptureMemoryPreference.Cpu
        };
        await this.mediaCapture.InitializeAsync(captureSettings);

        this.mediaSourceReaders = new mtMediaSourceReader[]
        {
          new mtMediaSourceReader(this.mediaCapture, MediaFrameSourceKind.Color, this.OnFrameArrived),
          new mtMediaSourceReader(this.mediaCapture, MediaFrameSourceKind.Depth, this.OnFrameArrived),
          new mtMediaSourceReader(this.mediaCapture, MediaFrameSourceKind.Custom, this.OnFrameArrived,
            DoesCustomSourceSupportPerceptionFormat)
        };

        necessarySourcesAvailable = 
          this.mediaSourceReaders.All(foo => foo.Initialise());

        if (necessarySourcesAvailable)
        {
          foreach (var foo in this.mediaSourceReaders)
          {
            await foo.OpenReaderAsync();
          }
        }
        else
        {
          this.mediaCapture.Dispose();
        }
      }
      return (necessarySourcesAvailable);
    }
    void OnFrameArrived(MediaFrameReader sender)
    {
      var frame = sender.TryAcquireLatestFrame();

      if (frame != null)
      {
        switch (frame.SourceKind)
        {
          case MediaFrameSourceKind.Custom:
            this.ProcessCustomFrame(frame);
            break;
          case MediaFrameSourceKind.Color:
            this.ProcessColorFrame(frame);
            break;
          case MediaFrameSourceKind.Infrared:
            break;
          case MediaFrameSourceKind.Depth:
            this.ProcessDepthFrame(frame);
            break;
          default:
            break;
        }
        frame.Dispose();
      }
    }
    void ProcessDepthFrame(MediaFrameReference frame)
    {
      if (this.colorCoordinateSystem != null)
      {
        this.depthColorTransform = frame.CoordinateSystem.TryGetTransformTo(
          this.colorCoordinateSystem);
      }     
    }
    void ProcessColorFrame(MediaFrameReference frame)
    {
      if (this.colorCoordinateSystem == null)
      {
        this.colorCoordinateSystem = frame.CoordinateSystem;
        this.colorIntrinsics = frame.VideoMediaFrame.CameraIntrinsics;
      }
      this.softwareBitmapEventArgs.Bitmap = frame.VideoMediaFrame.SoftwareBitmap;
      this.ColorFrameArrived?.Invoke(this, this.softwareBitmapEventArgs);
    }
    void ProcessCustomFrame(MediaFrameReference frame)
    {
      if ((this.PoseFrameArrived != null) &&
        (this.colorCoordinateSystem != null))
      {
        var trackingFrame = PoseTrackingFrame.Create(frame);
        var eventArgs = new mtPoseTrackingFrameEventArgs();

        if (trackingFrame.Status == PoseTrackingFrameCreationStatus.Success)
        {
          // Which of the entities here are actually tracked?
          var trackedEntities =
            trackingFrame.Frame.Entities.Where(e => e.IsTracked).ToArray();

          var trackedCount = trackedEntities.Count();

          if (trackedCount > 0)
          {
            eventArgs.PoseEntries =
              trackedEntities
              .Select(entity =>
                mtPoseTrackingDetails.FromPoseTrackingEntity(entity, this.colorIntrinsics, this.depthColorTransform.Value))
              .ToArray();
          }
          this.PoseFrameArrived(this, eventArgs);
        }
      }
    }
    async static Task<IEnumerable<MediaFrameSourceGroup>> GetGroupsSupportingSourceKindsAsync(
      params MediaFrameSourceKind[] kinds)
    {
      var sourceGroups = await MediaFrameSourceGroup.FindAllAsync();

      var groups =
        sourceGroups.Where(
          group => kinds.All(
            kind => group.SourceInfos.Any(sourceInfo => sourceInfo.SourceKind == kind)));

      return (groups);
    }
    static bool DoesCustomSourceSupportPerceptionFormat(MediaFrameSource source)
    {
      return (
        (source.Info.SourceKind == MediaFrameSourceKind.Custom) &&
        (source.CurrentFormat.MajorType == PerceptionFormat) &&
        (Guid.Parse(source.CurrentFormat.Subtype) == PoseTrackingFrame.PoseTrackingSubtype));
    }
    SpatialCoordinateSystem colorCoordinateSystem;
    mtSoftwareBitmapEventArgs softwareBitmapEventArgs;
    mtMediaSourceReader[] mediaSourceReaders;
    MediaCapture mediaCapture;
    CameraIntrinsics colorIntrinsics;
    const string PerceptionFormat = "Perception";
    private Matrix4x4? depthColorTransform;
  }
}