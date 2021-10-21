using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;

public class CameraController : MonoBehaviour
{
    private PointCloudLoader plyLoader;
    private Camera camera;

    private Camera Camera { get {
            if (!camera)
                camera = gameObject.GetComponent<Camera>();
            return camera;
        } }
     
    // Start is called before the first frame update
    void Start()
    {
        plyLoader = FindObjectOfType<PointCloudLoader>();
        Subscribe();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            OnClickMouseLeft();
    }

    private void Subscribe()
    {
        plyLoader.PointCloudSetting.isLoaded
            .DistinctUntilChanged()
            .Where(isLoad => isLoad)
            .Take(1)
            .Subscribe(_ => AlignCameraTransform()); 
    }

    private void AlignCameraTransform()
    {
        var pointCloudInfo = plyLoader.PointCloudInfo;
        var dir = (transform.up + transform.right + transform.forward).normalized;
        var distance = pointCloudInfo.Size.magnitude; 
        var newPos = pointCloudInfo.Center + (dir * distance);

        transform.position = newPos;
        transform.LookAt(pointCloudInfo.Center, Vector3.up);
    }

    private void OnClickMouseLeft()
    {
        var ray = Camera.ScreenPointToRay(Input.mousePosition);
        Vector3 resultWorldPos, resultLocalPos;
        var isSuccess = plyLoader.SearchNearestPoint(ray, out resultWorldPos, out resultLocalPos);
        if (!isSuccess)
            return;
        DataManager.Instance.PickingData.pickingWorldPoint.Value = resultWorldPos;
        DataManager.Instance.PickingData.pickingLocalPoint.Value = resultLocalPos;

    }
}
