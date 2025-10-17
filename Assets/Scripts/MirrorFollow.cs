using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class MirrorFollow : MonoBehaviour
{
    [Header("References")]
    public Camera playerCam;          // Main camera
    public Transform mirrorPlane;     // Quad (its +Z/forward faces the room)

    [Header("Options")]
    [Tooltip("Keep mirror camera Y equal to player's Y.")]
    public bool keepSameHeight = true;

    [Tooltip("Match FOV/near/far/ortho to player's lens.")]
    public bool matchLens = true;

    [Tooltip("Use reflected Up (keeps the same roll as player). If off, use world up.")]
    public bool reflectUpVector = true;

    [Header("Rendering")]
    [Tooltip("Small push to avoid z-fighting with the mirror surface.")]
    public float clipPlaneOffset = 0.01f;
    public bool useInvertCulling = true;

    // internals
    private Camera mirrorCam;
    private Vector3 startPos;
    private Quaternion startRot;
    private Matrix4x4 startProj;
    private bool cached;

    void OnEnable()
    {
        mirrorCam = GetComponent<Camera>();
        if (playerCam == null) playerCam = Camera.main;

        startPos = transform.position;
        startRot = transform.rotation;
        startProj = mirrorCam.projectionMatrix;
        cached = true;
    }

    void OnDisable() { Restore(); }
    void OnApplicationQuit() { Restore(); }
    void Restore()
    {
        if (!cached) return;
        transform.SetPositionAndRotation(startPos, startRot);
        mirrorCam.projectionMatrix = startProj;
        GL.invertCulling = false;
    }

    void LateUpdate()
    {
        if (playerCam == null || mirrorPlane == null) return;

        // Mirror plane: point and outward normal
        Vector3 p0 = mirrorPlane.position;
        Vector3 n = mirrorPlane.forward.normalized;   // MUST face out of the mirror

        // Player pose
        Transform pt = playerCam.transform;

        // True mirror: position is player's position reflected across the plane
        Vector3 mirroredPos = ReflectPoint(pt.position, p0, n);
        if (keepSameHeight) mirroredPos.y = pt.position.y;  // optional same-height lock
        transform.position = mirroredPos;

        // Orientation: reflect player's forward and up vectors
        Vector3 mf = ReflectVector(pt.forward, n);
        Vector3 mu = reflectUpVector ? ReflectVector(pt.up, n) : Vector3.up;
        transform.rotation = Quaternion.LookRotation(mf, mu);

        // Match lens if desired
        if (matchLens)
        {
            mirrorCam.fieldOfView = playerCam.fieldOfView;
            mirrorCam.nearClipPlane = playerCam.nearClipPlane;
            mirrorCam.farClipPlane = playerCam.farClipPlane;
            mirrorCam.orthographic = playerCam.orthographic;
            mirrorCam.orthographicSize = playerCam.orthographicSize;
        }

        // Oblique near clip so we don't see behind the mirror
        Vector4 planeWorld = new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, p0) - clipPlaneOffset);
        Matrix4x4 view = mirrorCam.worldToCameraMatrix;
        Vector4 planeCam = view.inverse.transpose * planeWorld;

        mirrorCam.ResetProjectionMatrix();
        mirrorCam.projectionMatrix = mirrorCam.CalculateObliqueMatrix(planeCam); // Unity 6 instance method
    }

    void OnPreRender() { if (useInvertCulling) GL.invertCulling = true; }
    void OnPostRender() { if (useInvertCulling) GL.invertCulling = false; }

    // helpers
    static Vector3 ReflectPoint(Vector3 p, Vector3 p0, Vector3 n)
        => p - 2f * Vector3.Dot(n, p - p0) * n;

    static Vector3 ReflectVector(Vector3 v, Vector3 n)
        => v - 2f * Vector3.Dot(n, v) * n;
}
