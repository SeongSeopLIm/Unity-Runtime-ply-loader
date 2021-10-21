using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Utility : PersistentSingleton<Utility>
{
    /// <summary>
    /// Update문에서 실행할 이벤트 데이터입니다.
    /// </summary>
    public class MainTask
    {
        public Action method;
        public float timer;

        public bool IsFinish => timer <= 0;

        public MainTask(float timer, Action method)
        {
            this.timer = timer;
            this.method = method;
        }

        public void Update(float deltaTime)
        {
            timer -= deltaTime;
        }

        public void Invoke()
        {
            method?.Invoke();
        }
    }

    private List<MainTask> mainTasks = new List<MainTask>();

    private void Update()
    {
        CheckMainTasks();
    }

    /// <summary>
    /// 재생 가능한 이벤트 데이터를 체크합니다.
    /// </summary>
    void CheckMainTasks()
    {
        var deltaTime = Time.deltaTime;
        int i = mainTasks.Count - 1;
        for (; i >= 0; i--)
        {
            var task = mainTasks[i];
            task.Update(deltaTime);
            if (!task.IsFinish)
                continue;

            task.Invoke();
            mainTasks.RemoveAt(i);
        }
    }

    /// <summary>
    /// 다음 Update문에서 실행할 메소드를 추가합니다.
    /// </summary>
    /// <param name="newTask">메소드의 데이터입니다.</param>
    public void AddMainTask(MainTask newTask)
    {
        mainTasks.Add(newTask);
    }


    /// <summary>
    /// 마우스가 UI에 올라와있는지 판별합니다.
    /// </summary>
    /// <returns>UI 위에 마우스가 있을 경우 true를 반환합니다.</returns>
    public static bool IsPointerOverUIObject()
    {
        if (EventSystem.current.IsPointerOverGameObject())
            return true;
        PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
        eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        List<RaycastResult> results = new List<RaycastResult>();

        EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

        return results.Count > 0;
    }


    public static bool IsPointerOverUIObject(GameObject uiGameObject)
    {
        if (EventSystem.current.IsPointerOverGameObject())
            return true;
        PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
        eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        List<RaycastResult> results = new List<RaycastResult>();

        EventSystem.current.RaycastAll(eventDataCurrentPosition, results);
        var isFind = results.FindIndex((iter) => iter.gameObject == uiGameObject) != -1;

        return isFind;
    }

}