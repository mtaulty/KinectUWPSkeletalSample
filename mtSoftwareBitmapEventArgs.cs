namespace KinectTestApp
{
  using System;
  using Windows.Graphics.Imaging;

  class mtSoftwareBitmapEventArgs : EventArgs
  {
    public SoftwareBitmap Bitmap { get; set; }
  }
}
