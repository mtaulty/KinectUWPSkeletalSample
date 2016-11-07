namespace KinectTestApp
{
  using Microsoft.Graphics.Canvas;
  using Microsoft.Graphics.Canvas.UI.Xaml;
  using System.Numerics;
  using System.Threading;
  using Windows.Foundation;
  using Windows.Graphics.Imaging;
  using Windows.UI;
  using Windows.UI.Core;
  using Windows.UI.Xaml;
  using Windows.UI.Xaml.Controls;

  public sealed partial class MainPage : Page
  {
    public MainPage()
    {
      this.InitializeComponent();
      this.Loaded += this.OnLoaded;
    }
    void OnCanvasControlSizeChanged(object sender, SizeChangedEventArgs e)
    {
      this.canvasSize = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
    }
    async void OnLoaded(object sender, RoutedEventArgs e)
    {
      this.helper = new mtKinectColorPoseFrameHelper();

      this.helper.ColorFrameArrived += OnColorFrameArrived;
      this.helper.PoseFrameArrived += OnPoseFrameArrived;

      var suppported = await this.helper.InitialiseAsync();

      if (suppported)
      {
        this.canvasControl.Visibility = Visibility.Visible;
      }
    }
    void OnColorFrameArrived(object sender, mtSoftwareBitmapEventArgs e)
    {
      // Note that when this function returns to the caller, we have
      // finished with the incoming software bitmap.
      if (this.bitmapSize == null)
      {
        this.bitmapSize = new Rect(0, 0, e.Bitmap.PixelWidth, e.Bitmap.PixelHeight);
      }

      if (Interlocked.CompareExchange(ref this.isBetweenRenderingPass, 1, 0) == 0)
      {
        this.lastConvertedColorBitmap?.Dispose();

        // Sadly, the format that comes in here, isn't supported by Win2D when
        // it comes to drawing so we have to convert. The upside is that 
        // we know we can keep this bitmap around until we are done with it.
        this.lastConvertedColorBitmap = SoftwareBitmap.Convert(
          e.Bitmap,
          BitmapPixelFormat.Bgra8,
          BitmapAlphaMode.Ignore);

        // Cause the canvas control to redraw itself.
        this.InvalidateCanvasControl();
      }
    }
    void InvalidateCanvasControl()
    {
      // Fire and forget.
      this.Dispatcher.RunAsync(CoreDispatcherPriority.High, this.canvasControl.Invalidate);
    }
    void OnPoseFrameArrived(object sender, mtPoseTrackingFrameEventArgs e)
    {
      // NB: we do not invalidate the control here but, instead, just keep
      // this frame around (maybe) until the colour frame redraws which will 
      // (depending on race conditions) pick up this frame and draw it
      // too.
      this.lastPoseEventArgs = e;
    }
    void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
      // Capture this here (in a race) in case it gets over-written
      // while this function is still running.
      var poseEventArgs = this.lastPoseEventArgs;

      args.DrawingSession.Clear(Colors.Black);

      // Do we have a colour frame to draw?
      if (this.lastConvertedColorBitmap != null)
      {
        using (var canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(
          this.canvasControl,
          this.lastConvertedColorBitmap))
        {
          // Draw the colour frame
          args.DrawingSession.DrawImage(
            canvasBitmap,
            this.canvasSize,
            this.bitmapSize.Value);

          // Have we got a skeletal frame hanging around?
          if (poseEventArgs?.PoseEntries?.Length > 0)
          {
            foreach (var entry in poseEventArgs.PoseEntries)
            {
              foreach (var pose in entry.Points)
              {
                var centrePoint = ScalePosePointToDrawCanvasVector2(pose);

                args.DrawingSession.FillCircle(
                  centrePoint, circleRadius, Colors.Red);
              }
            }
          }
        }
      }
      Interlocked.Exchange(ref this.isBetweenRenderingPass, 0);
    }
    Vector2 ScalePosePointToDrawCanvasVector2(Point posePoint)
    {
      return (new Vector2(
        (float)((posePoint.X / this.bitmapSize.Value.Width) * this.canvasSize.Width),
        (float)((posePoint.Y / this.bitmapSize.Value.Height) * this.canvasSize.Height)));
    }
    Rect? bitmapSize;
    Rect canvasSize;
    int isBetweenRenderingPass;
    SoftwareBitmap lastConvertedColorBitmap;
    mtPoseTrackingFrameEventArgs lastPoseEventArgs;
    mtKinectColorPoseFrameHelper helper;
    static readonly float circleRadius = 10.0f;
  }
}

