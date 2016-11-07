namespace KinectTestApp
{
  using System;
  using System.Linq;
  using System.Numerics;
  using Windows.Foundation;
  using Windows.Media.Devices.Core;
  using WindowsPreview.Media.Capture.Frames;

  class mtPoseTrackingDetails
  {
    public Guid EntityId { get; set; }
    public Point[] Points { get; set; }

    public static mtPoseTrackingDetails FromPoseTrackingEntity(
      PoseTrackingEntity poseTrackingEntity,
      CameraIntrinsics colorIntrinsics,
      Matrix4x4 depthColorTransform)
    {
      mtPoseTrackingDetails details = null;

      var poses = new TrackedPose[poseTrackingEntity.PosesCount];
      poseTrackingEntity.GetPoses(poses);

      var points = new Point[poses.Length];

      colorIntrinsics.ProjectManyOntoFrame(
        poses.Select(p => Multiply(depthColorTransform, p.Position)).ToArray(),
        points);

      details = new mtPoseTrackingDetails()
      {
        EntityId = poseTrackingEntity.EntityId,
        Points = points
      };
      return (details);
    }
    static Vector3 Multiply(Matrix4x4 matrix, Vector3 position)
    {
      return (new Vector3(
        position.X * matrix.M11 + position.Y * matrix.M21 + position.Z * matrix.M31 + matrix.M41,
        position.X * matrix.M12 + position.Y * matrix.M22 + position.Z * matrix.M32 + matrix.M42,
        position.X * matrix.M13 + position.Y * matrix.M23 + position.Z * matrix.M33 + matrix.M43));
    }
  }
}
