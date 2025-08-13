using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragImage : MonoBehaviour, IPointerClickHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IEndDragHandler
{
    RectTransform rectTransform;
    public Image image;
    float dragSpeed = 0.02f;
    public bool scale = false, changed = false;
    public int index, id;
    public List<Transform> walls;
    public float currentWidth, nextWidth;

    public Vector2 curDist, prevDist;
    public float touchDelta, speedTouch0, speedTouch1;
    public float speed = 0.3f;
    public float MINSCALE = 2.0F;
    public float MAXSCALE = 5.0F;
    public float minPinchSpeed = 5.0F;
    public float varianceInDistances = 5.0F;

    private void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        CalcWidths();
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
        if (scale && Helper.mobile)
        {
            if (Input.touchCount == 2 && Input.GetTouch(0).phase == TouchPhase.Moved && Input.GetTouch(1).phase == TouchPhase.Moved)
            {
                curDist = Input.GetTouch(0).position - Input.GetTouch(1).position; //current distance between finger touches
                prevDist = ((Input.GetTouch(0).position - Input.GetTouch(0).deltaPosition) - (Input.GetTouch(1).position - Input.GetTouch(1).deltaPosition)); //difference in previous locations using delta positions
                touchDelta = curDist.magnitude - prevDist.magnitude;
                speedTouch0 = Input.GetTouch(0).deltaPosition.magnitude / Input.GetTouch(0).deltaTime;
                speedTouch1 = Input.GetTouch(1).deltaPosition.magnitude / Input.GetTouch(1).deltaTime;
                var ls = rectTransform.localScale;
                var d = ls.x;
                if ((touchDelta + varianceInDistances <= 1) && (speedTouch0 > minPinchSpeed) && (speedTouch1 > minPinchSpeed))
                {
                    d = Mathf.Clamp(d + (1 * speed), 1, 30);
                }
                if ((touchDelta + varianceInDistances > 1) && (speedTouch0 > minPinchSpeed) && (speedTouch1 > minPinchSpeed))
                {
                    d = Mathf.Clamp(d - (1 * speed), 1, 30);
                }
                ls = new Vector3(d, d, 0);
                if (ls.x > 0.2f && ls.x < 5) rectTransform.localScale = ls;
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!Helper.moveLock)
        {
            if (Helper.init.image != null)
            {
                Helper.init.image.image.color = new Color32(255, 255, 255, 255);
                Helper.init.image.scale = false;
            }
            Helper.init.ShowCursor();
            image.color = new Color32(0, 202, 255, 255);
            scale = true;
            Helper.init.image = this;
            Helper.init.jump.SetActive(false);
            Helper.init.ok.SetActive(true);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!Helper.mobile)
        {
            image.color = new Color32(0, 202, 255, 255);
            scale = true;
        }
    }

    public async void OnPointerExit(PointerEventData eventData)
    {
        if (!Helper.mobile)
        {
            image.color = new Color32(255, 255, 255, 255);
            scale = false;
            Save();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Helper.mobile && Input.touchCount == 2) return;
        try
        {
            image.color = new Color32(0, 202, 255, 255);
            //Debug.Log($"cursor position: {eventData.position.x}; {eventData.position.y}; image position: {rectTransform.position.x}; {rectTransform.position.y}");
            changed = true;
            float x = eventData.delta.x * dragSpeed;
            float y = eventData.delta.y * dragSpeed;
            rectTransform.anchoredPosition += new Vector2(x, y);
            var w = rectTransform.sizeDelta.x;
            var s = rectTransform.localScale.x;
            var h = rectTransform.sizeDelta.y * s;
            var p = w * s;
            if (rectTransform.anchoredPosition.x + p > currentWidth)
            {
                index++;
                if (index >= walls.Count) index = 0;
                transform.SetParent(walls[index]);
                rectTransform.localPosition = new Vector2(0, rectTransform.localPosition.y);
                rectTransform.anchoredPosition = new Vector2(0, rectTransform.anchoredPosition.y);
                rectTransform.localEulerAngles = Vector3.zero;
                CalcWidths();
            }
            else if (rectTransform.anchoredPosition.x < 0)
            {
                index--;
                if (index < 0) index = walls.Count - 1;
                transform.SetParent(walls[index]);
                rectTransform.localPosition = new Vector2(nextWidth - p, rectTransform.localPosition.y);
                rectTransform.anchoredPosition = new Vector2(nextWidth - p, rectTransform.anchoredPosition.y);
                rectTransform.localEulerAngles = Vector3.zero;
                CalcWidths();
            }
            if (rectTransform.anchoredPosition.y > -h) rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, -h);
            if (rectTransform.anchoredPosition.y < -10) rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, -10);
        }
        catch { }
    }

    void CalcWidths()
    {
        if (index == 0 || index == 2) { currentWidth = 20; nextWidth = 15; }
        else { currentWidth = 15; nextWidth = 20; }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Cursor.visible = false;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Cursor.visible = true;
        if (!Helper.mobile) image.color = new Color32(255, 255, 255, 255);
        Save();
    }

    public void Save()
    {
        if (changed)
        {
            changed = false;
            string x = rectTransform.anchoredPosition.x.ToString().Replace(",", ".");
            string y = rectTransform.anchoredPosition.y.ToString().Replace(",", ".");
            string s = rectTransform.localScale.x.ToString().Replace(",", ".");
            //StartCoroutine(Helper.LoadFromServer($"http://localhost:20057/room/SetPosition?id={id}&x={x}&y={y}&scale={s}&index={index}"));
            StartCoroutine(Helper.LoadFromServer($"https://rooms.worldofton.ru/room/SetPosition?id={id}&x={x}&y={y}&scale={s}&index={index}"));
        }
    }
}
