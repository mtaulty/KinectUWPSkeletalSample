namespace KinectTestApp
{
  using System;

  class mtPoseTrackingFrameEventArgs : EventArgs
  {
    public mtPoseTrackingDetails[] PoseEntries { get; set; }
  }
}
