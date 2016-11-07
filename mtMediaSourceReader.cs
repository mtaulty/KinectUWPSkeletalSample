namespace KinectTestApp
{
  using System;
  using System.Linq;
  using System.Threading.Tasks;
  using Windows.Media.Capture;
  using Windows.Media.Capture.Frames;

  class mtMediaSourceReader
  {
    public mtMediaSourceReader(
      MediaCapture capture, 
      MediaFrameSourceKind mediaSourceKind,
      Action<MediaFrameReader> onFrameArrived,
      Func<MediaFrameSource, bool> additionalSourceCriteria = null)
    {
      this.mediaCapture = capture;
      this.mediaSourceKind = mediaSourceKind;
      this.additionalSourceCriteria = additionalSourceCriteria;
      this.onFrameArrived = onFrameArrived;
    }
    public bool Initialise()
    {
      this.mediaSource = this.mediaCapture.FrameSources.FirstOrDefault(
        fs =>
          (fs.Value.Info.SourceKind == this.mediaSourceKind) &&
          ((this.additionalSourceCriteria != null) ? 
            this.additionalSourceCriteria(fs.Value) : true)).Value;   

      return (this.mediaSource != null);
    }
    public async Task OpenReaderAsync()
    {
      this.frameReader =
        await this.mediaCapture.CreateFrameReaderAsync(this.mediaSource);

      this.frameReader.FrameArrived +=
        (s, e) =>
        {
          this.onFrameArrived(s);
        };

      await this.frameReader.StartAsync();
    }
    Func<MediaFrameSource, bool> additionalSourceCriteria;
    Action<MediaFrameReader> onFrameArrived;
    MediaFrameReader frameReader;
    MediaFrameSource mediaSource;
    MediaCapture mediaCapture;
    MediaFrameSourceKind mediaSourceKind;
  }
}
