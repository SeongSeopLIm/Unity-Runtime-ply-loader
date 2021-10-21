using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using UniRx.Triggers;


public class MainRootUI : MonoBehaviour
{
    private enum UIState
    {
        PreLoad = 0,
        Loading,
        CompleteLoad,
    }

    [SerializeField] private Slider slider;
    [SerializeField] private Text loadingText;
    [SerializeField] private Text pickingPointText;
    [SerializeField] private InputField rotX;
    [SerializeField] private InputField rotY;
    [SerializeField] private InputField rotZ;
    [SerializeField] private Slider sizeSlider;
    [SerializeField] private GameObject[] uiRoots;

    private PointCloudLoader plyLoader;
    private ReactiveProperty<UIState> currentUIState = new ReactiveProperty<UIState>(UIState.PreLoad);
    private string pickingTextFormat = "picking point : {0}";

    // Start is called before the first frame update
    void Start()
    {
        plyLoader = FindObjectOfType<PointCloudLoader>();
        Subscribe();

    }
    private void Subscribe()
    {
        plyLoader.isLoading
            .DistinctUntilChanged()
            .Subscribe(isLoading =>
            {
                if(plyLoader.PointCloudSetting.isLoaded.Value)
                {
                    currentUIState.Value = UIState.CompleteLoad;
                }
                else
                {
                    currentUIState.Value = isLoading ? UIState.Loading : UIState.PreLoad;
                }
            })
            .AddTo(this);


        var loadObserve = plyLoader.PointCloudSetting.isLoaded
            .DistinctUntilChanged()
            .Where(isLoad => isLoad)
            .Take(1);

        loadObserve
            .Subscribe(_ => currentUIState.Value = UIState.CompleteLoad)
            .AddTo(this);

        this
            .UpdateAsObservable()
            .TakeUntil(loadObserve)
            .Subscribe(_ => UpdateLoading())
            .AddTo(this);

        currentUIState
            .DistinctUntilChanged()
            .Subscribe(state =>
            {
                for(int i = 0; i < uiRoots.Length; i++)
                {
                    var isActive = (int)state == i;
                    uiRoots[i].SetActive(isActive);
                } 
            });

        DataManager.Instance.PickingData.pickingLocalPoint
            .DistinctUntilChanged()
            .Subscribe(pos => pickingPointText.text = string.Format(pickingTextFormat, pos.ToString()));

        var xObserve = rotX.OnEndEditAsObservable();
        var yObserve = rotY.OnEndEditAsObservable();
        var zObserve = rotZ.OnEndEditAsObservable();

        Observable.Merge(xObserve, yObserve, zObserve)
            .Subscribe(_ => ApplyPCDRotaion());

        sizeSlider
            .OnValueChangedAsObservable()
            .DistinctUntilChanged()
            .Subscribe(size => plyLoader.PointCloudSetting.pointSize.Value = size);

    }



    private void UpdateLoading()
    {
        loadingText.text = plyLoader.ProgressTitle;
        slider.value = plyLoader.LoadProgress.Value;
    }

    private void ApplyPCDRotaion()
    {
        float x, y, z;

        var isSuccess = float.TryParse(rotX.text, out x);
        if (!isSuccess)
            return;
        isSuccess = float.TryParse(rotY.text, out y);
        if (!isSuccess)
            return;
        isSuccess = float.TryParse(rotZ.text, out z);
        if (!isSuccess)
            return;

        plyLoader.PointCloudSetting.rotation.Value = new Vector3(x, y, z);

    }

}
