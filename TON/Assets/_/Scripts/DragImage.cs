using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragImage : MonoBehaviour, IPointerClickHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    RectTransform rectTransform;
    Image image;
    float dragSpeed = 0.01f;
    bool scale = false, changed = false;
    public string url;
    public int index;
    public List<Transform> walls;

    private void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
    }

    void Update()
    {
        if (EventSystem.current.IsPointerOverGameObject())
        {
            var d = Input.GetAxis("Mouse ScrollWheel");
            if (d != 0 && scale)
            {
                changed = true;
                var ls = rectTransform.localScale;
                ls += new Vector3(d, d, 0);
                if (ls.x > 0.2f && ls.x < 5) rectTransform.localScale = ls;
            }
        }
        if (Input.GetMouseButtonUp(0))
        {
            Cursor.visible = true;
            image.color = new Color32(255, 255, 255, 255);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        scale = true;
    }

    public async void OnPointerExit(PointerEventData eventData)
    {
        scale = false;
        if (changed)
        {
            changed = false;
            string x = rectTransform.anchoredPosition.x.ToString().Replace(",", ".");
            string y = rectTransform.anchoredPosition.y.ToString().Replace(",", ".");
            string s = rectTransform.localScale.x.ToString().Replace(",", ".");
            await Helper.Post("http://45.132.107.107/index/SetPosition", $"address={PlayerPrefs.GetString("address")}&image={url}&x={x}&y={y}&scale={s}&index={index}");
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        Cursor.visible = false;
        image.color = new Color32(0, 202, 255, 255);
        //Debug.Log($"cursor position: {eventData.position.x}; {eventData.position.y}; image position: {rectTransform.position.x}; {rectTransform.position.y}");
        changed = true;
        float x = eventData.delta.x * dragSpeed;
        float y = eventData.delta.y * dragSpeed;
        rectTransform.anchoredPosition += new Vector2(x, y);
        var w = rectTransform.sizeDelta.x;
        var h = rectTransform.sizeDelta.y;
        var s = rectTransform.localScale.x;
        var p = w / 2 * s;
        if (rectTransform.anchoredPosition.x + p > 5.8f)
        {
            index++;
            if (index >= walls.Count) index = 0;
            transform.SetParent(walls[index]);
            rectTransform.localPosition = new Vector2(p, rectTransform.localPosition.y);
            rectTransform.anchoredPosition = new Vector2(p, rectTransform.anchoredPosition.y);
            rectTransform.localEulerAngles = Vector3.zero;
        }
        else if (rectTransform.anchoredPosition.x - p < 0)
        {
            index--;
            if (index < 0) index = walls.Count - 1;
            transform.SetParent(walls[index]);
            rectTransform.localPosition = new Vector2(5.8f - p, rectTransform.localPosition.y);
            rectTransform.anchoredPosition = new Vector2(5.8f - p, rectTransform.anchoredPosition.y);
            rectTransform.localEulerAngles = Vector3.zero;
        }
    }
}
