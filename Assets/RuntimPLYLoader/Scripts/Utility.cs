using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Utility : PersistentSingleton<Utility>
{
    /// <summary>
    /// Update������ ������ �̺�Ʈ �������Դϴ�.
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
    /// ��� ������ �̺�Ʈ �����͸� üũ�մϴ�.
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
    /// ���� Update������ ������ �޼ҵ带 �߰��մϴ�.
    /// </summary>
    /// <param name="newTask">�޼ҵ��� �������Դϴ�.</param>
    public void AddMainTask(MainTask newTask)
    {
        mainTasks.Add(newTask);
    }


    /// <summary>
    /// ���콺�� UI�� �ö���ִ��� �Ǻ��մϴ�.
    /// </summary>
    /// <returns>UI ���� ���콺�� ���� ��� true�� ��ȯ�մϴ�.</returns>
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