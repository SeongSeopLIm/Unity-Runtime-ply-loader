using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UniRx;

public class DataManager : PersistentSingleton<DataManager>
{
    public PickingData PickingData = new PickingData();

}

#region DataModel

public class PickingData
{
    public ReactiveProperty<Vector3> pickingWorldPoint = new ReactiveProperty<Vector3>(Vector3.zero);
    public ReactiveProperty<Vector3> pickingLocalPoint = new ReactiveProperty<Vector3>(Vector3.zero);
}


#endregion
